/*
必要な NuGet パッケージ:
dotnet add package Microsoft.Agents.AI --prerelease
dotnet add package Azure.AI.OpenAI
dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
dotnet add package ModelContextProtocol --prerelease
 */

// Azure OpenAI のクライアント用
using Azure;
using Azure.AI.OpenAI;
// Microsoft Agent Framework 用
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
// MCP クライアントとツールを利用するための名前空間
using ModelContextProtocol.Client;
using System.Text.Json;

namespace FCAIChat.AIAgents;

/// <summary>AI エージェントの基底クラス</summary>
/// <remarks>
/// <para>
/// DI にシングルトンとして登録し、アプリ全体で 1 つの AIAgent / IChatClient / MCP クライアントを共有する。
/// 会話の状態 (履歴など) は AgentSession が持つので、呼び出し側がシリアライズしたセッションを受け渡す。
/// </para>
/// <para>
/// 派生クラスでは、エージェント名 (<see cref="Name"/>)・指示 (<see cref="Instructions"/>)・
/// 使用する LLM (<see cref="GetChatClient"/>)・使用するツール (<see cref="GetMcpToolsAsync"/>) を決める。
/// </para>
/// </remarks>
/// <param name="logger">ログ出力先</param>
public abstract class ChatAgent(ILogger logger) : IAsyncDisposable
{
    // AIAgent の生成を 1 回に限定するためのロック
    readonly SemaphoreSlim startLock  = new(1, 1);
    // 使用中の MCP クライアント (破棄時に DisposeAsync する)
    IEnumerable<McpClient> mcpClients = [];
    // 初回の呼び出し時に生成する AIAgent (生成前は null)
    AIAgent?               agent      = null;

    /// <summary>ログ出力先</summary>
    protected ILogger Logger { get; } = logger;

    /// <summary>エージェント名 (チャットでは "@エージェント名" で呼び出す)</summary>
    public abstract string    Name         { get; }
    /// <summary>エージェントへの指示 (システムプロンプト)</summary>
    protected abstract string Instructions { get; }

    /// <summary>MCP クライアントを破棄する (アプリ終了時に DI コンテナから呼ばれる)</summary>
    public async ValueTask DisposeAsync()
    {
        // 終了処理 MCP クライアントを破棄 (stdio で起動した MCP サーバーのプロセスも終了する)
        foreach (var mcpClient in mcpClients)
            await mcpClient.DisposeAsync();
        mcpClients = [];
        startLock.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>ユーザープロンプトに対する応答を取得する</summary>
    /// <param name="userPrompt">ユーザープロンプト</param>
    /// <param name="serializedSession">前回までの会話 (なければ null)</param>
    /// <returns>応答と、更新後の会話 (失敗時は null)</returns>
    /// <remarks>例外は投げず、失敗時はログに記録したうえでエラーメッセージを応答として返す。</remarks>
    public async Task<(string response, JsonElement? session)> GetResponseAsync(string userPrompt, JsonElement? serializedSession)
    {
        try {
            var agent   = await GetAgentAsync();
            // 前回までの会話を復元し、その続きとして問い合わせる
            var session = await GetSessionAsync(agent, serializedSession);
            // ユーザープロンプトをエージェントセッションに送信し、応答を取得
            // (ツール呼び出しが必要なら、IChatClient の UseFunctionInvocation によって自動で実行される)
            var response = await agent.RunAsync(new ChatMessage(ChatRole.User, userPrompt), session);
            // 今回のやり取りを含めた会話をシリアライズして返す (呼び出し側が保存する)
            return (response?.Text ?? string.Empty, await agent.SerializeSessionAsync(session));
        } catch (Exception ex) {
            Logger.LogError(ex, "Error running agent {AgentName}", Name);
            return ("(エラーが発生したため応答できませんでした)", null);
        }
    }

    /// <summary>使用する LLM のクライアントを生成する (ファクトリ関数)</summary>
    protected abstract IChatClient GetChatClient();

    /// <summary>エージェントが使う MCP サーバーのツールを取得する</summary>
    /// <returns>
    /// 接続した MCP クライアントと、そのツール一覧。
    /// MCP クライアントはこのエージェントの破棄時に DisposeAsync() される。
    /// 既定ではツールを使わない。
    /// </returns>
    protected virtual Task<(IEnumerable<McpClient>, IEnumerable<McpClientTool>)> GetMcpToolsAsync()
        => Task.FromResult<(IEnumerable<McpClient>, IEnumerable<McpClientTool>)>(([], []));

    // 初回のみ AIAgent を生成して返す。2 回目以降は生成済みのものを返す
    // - 複数の接続から同時に呼ばれても 1 回だけ生成するよう、ダブルチェックロッキングを行う
    // - 生成に失敗した場合 (環境変数の未設定など) は agent が null のままなので、次回の呼び出しで再試行される
    async Task<AIAgent> GetAgentAsync()
    {
        if (agent is not null)
            return agent;

        await startLock.WaitAsync();
        try {
            // ロック待ちの間に他のスレッドが生成済みかもしれないので、もう一度確認する
            if (agent is not null)
                return agent;

            var (mcpClients, tools) = await GetMcpToolsAsync();
            this.mcpClients = mcpClients;
            return agent = CreateAgent(tools);
        } finally {
            startLock.Release();
        }

        // LLM のクライアント・指示・ツールを組み合わせて AIAgent を生成する
        AIAgent CreateAgent(IEnumerable<McpClientTool> tools)
            => new ChatClientAgent(
                chatClient  : GetChatClient(),
                instructions: Instructions,
                name        : Name,
                description : null,
                tools       : tools.Any() ? [.. tools] : null
            );
    }

    // 保存済みの会話を復元する。なければ (または復元に失敗したら) 新しく作る
    async Task<AgentSession> GetSessionAsync(AIAgent agent, JsonElement? serializedSession)
    {
        if (serializedSession is { } session) {
            try {
                return await agent.DeserializeSessionAsync(session);
            } catch (Exception ex) {
                // 復元できなくても応答はできるので、警告を記録して新しい会話として続ける
                Logger.LogWarning(ex, "Session restoration failed. A new session will be created.");
            }
        }
        return await agent.CreateSessionAsync();
    }
}

/// <summary>このアプリで使う AI エージェント</summary>
/// <remarks>Azure OpenAI のモデルを使い、Brave Search の MCP サーバーで Web 検索ができる。</remarks>
/// <param name="options">接続設定 (エンドポイント / API キー / デプロイ名など)</param>
/// <param name="logger">ログ出力先</param>
public class MyChatAgent(IOptions<ChatAgentOptions> options, ILogger<MyChatAgent> logger) : ChatAgent(logger)
{
    // 接続設定
    readonly ChatAgentOptions settings = options.Value;

    /// <inheritdoc/>
    public override string    Name         => "Agent";
    /// <inheritdoc/>
    /// <remarks>グループチャットであることと、発言が「名前: 本文」の形式で届くことを伝えている (ChatHub.SendMessage を参照)。</remarks>
    protected override string Instructions => """
        あなたはグループチャットに参加している AI エージェント「Agent」です。
        参加者の発言は「名前: 本文」の形式で届きます。発言者を区別して応答してください。
        必要に応じて Web 検索ツールを使い、日本語で簡潔に答えてください。
        """;

    /// <summary>Azure OpenAI のチャットクライアントを生成する</summary>
    /// <exception cref="InvalidOperationException">エンドポイントまたは API キーが設定されていない場合</exception>
    protected override IChatClient GetChatClient()
    {
        var azureOpenAIEndPoint = GetRequired(settings.Endpoint, "AZURE_OPENAI_ENDPOINT");
        var openAIApiKey        = GetRequired(settings.ApiKey  , "AZURE_OPENAI_API_KEY" );
        var credential          = new AzureKeyCredential(openAIApiKey);

        var azureOpenAIClient = new AzureOpenAIClient(new Uri(azureOpenAIEndPoint), credential);
        // IChatClient インターフェイスに変換して、ツール呼び出しを有効にしてビルド
        IChatClient chatClient = azureOpenAIClient.GetChatClient(settings.DeploymentName)
                                                  .AsIChatClient()
                                                  .AsBuilder()
                                                  .UseFunctionInvocation() // ツール呼び出しを使う
                                                  .Build();
        return chatClient;
    }

    /// <summary>Brave Search の MCP サーバーに接続し、ツール一覧を取得する</summary>
    /// <remarks>MCP サーバーは npx で子プロセスとして起動し、標準入出力 (stdio) で通信する。Node.js が必要。</remarks>
    protected override async Task<(IEnumerable<McpClient>, IEnumerable<McpClientTool>)> GetMcpToolsAsync()
    {
        IClientTransport clientTransport = GetBraveSearchToolClientTransport();
        McpClient client = await McpClient.CreateAsync(clientTransport);
        IList<McpClientTool> tools = await client.ListToolsAsync();
        foreach (var tool in tools)
            Logger.LogDebug("MCP tool: {ToolName} ({ToolDescription})", tool.Name, tool.Description);

        return ([client], tools);

        //// Playwright ツールを使うためのクライアント生成 (ブラウザー操作をさせたい場合の例)
        //static IClientTransport GetPlaywrightToolClientTransport()
        //    => new StdioClientTransport(new() {
        //        Name      = "playwright-mcp",
        //        Command   = "npx"           ,
        //        Arguments = ["@playwright/mcp@latest"]
        //    });

        // BraveSearch ツールを使うためのクライアント生成
        // - npx -y で取得されるパッケージはバージョンを固定する (サプライチェーン対策)
        // - API キーは MCP サーバーのプロセスに環境変数として渡す
        IClientTransport GetBraveSearchToolClientTransport()
            => new StdioClientTransport(new() {
                   Name                 = "braveSearch",
                   Command              = "npx",
                   Arguments            = ["-y", "@modelcontextprotocol/server-brave-search@0.6.2"],
                   EnvironmentVariables = new Dictionary<string, string?> {
                       ["BRAVE_API_KEY"] = GetRequired(settings.BraveApiKey, "BRAVE_API_KEY")
                   }
               });
    }

    // 必須の設定値を返す。未設定なら設定方法を示す例外を投げる
    // - セキュリティ上、エンドポイントや API キーはソースコードに書かず、構成 (User Secrets / 環境変数など) から取得する
    static string GetRequired(string value, string environmentVariable)
        => string.IsNullOrEmpty(value)
           ? throw new InvalidOperationException($"Please set the {environmentVariable} environment variable (or {ChatAgentOptions.SectionName} configuration).")
           : value;
}
