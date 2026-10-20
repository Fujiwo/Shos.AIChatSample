/*
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

/// <summary>AI エージェント</summary>
/// <remarks>
/// DI にシングルトンとして登録し、アプリ全体で 1 つの AIAgent / IChatClient / MCP クライアントを共有する。
/// 会話の状態は AgentSession が持つので、呼び出し側がシリアライズしたセッションを受け渡す。
/// </remarks>
public abstract class ChatAgent(ILogger logger) : IAsyncDisposable
{
    readonly SemaphoreSlim startLock  = new(1, 1);
    IEnumerable<McpClient> mcpClients = [];
    AIAgent?               agent      = null;

    protected ILogger Logger { get; } = logger;

    // エージェント名と指示
    public abstract string    Name         { get; }
    protected abstract string Instructions { get; }

    public async ValueTask DisposeAsync()
    {
        // 終了処理 MCP クライアントを破棄
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
    public async Task<(string response, JsonElement? session)> GetResponseAsync(string userPrompt, JsonElement? serializedSession)
    {
        try {
            var agent   = await GetAgentAsync();
            var session = await GetSessionAsync(agent, serializedSession);
            // ユーザープロンプトをエージェントセッションに送信し、応答を取得
            var response = await agent.RunAsync(new ChatMessage(ChatRole.User, userPrompt), session);
            return (response?.Text ?? string.Empty, await agent.SerializeSessionAsync(session));
        } catch (Exception ex) {
            Logger.LogError(ex, "Error running agent {AgentName}", Name);
            return ("(エラーが発生したため応答できませんでした)", null);
        }
    }

    // ファクトリ関数
    protected abstract IChatClient GetChatClient();

    // MCP サーバーのツールを取得
    // - 戻り値の McpClient はこのエージェントの破棄時に DisposeAsync() される
    protected virtual Task<(IEnumerable<McpClient>, IEnumerable<McpClientTool>)> GetMcpToolsAsync()
        => Task.FromResult<(IEnumerable<McpClient>, IEnumerable<McpClientTool>)>(([], []));

    // 初回のみエージェントを生成 (複数の接続から同時に呼ばれても 1 回だけ生成する)
    async Task<AIAgent> GetAgentAsync()
    {
        if (agent is not null)
            return agent;

        await startLock.WaitAsync();
        try {
            if (agent is not null)
                return agent;

            var (mcpClients, tools) = await GetMcpToolsAsync();
            this.mcpClients = mcpClients;
            return agent = CreateAgent(tools);
        } finally {
            startLock.Release();
        }

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
                Logger.LogWarning(ex, "Session restoration failed. A new session will be created.");
            }
        }
        return await agent.CreateSessionAsync();
    }
}

public class MyChatAgent(IOptions<ChatAgentOptions> options, ILogger<MyChatAgent> logger) : ChatAgent(logger)
{
    readonly ChatAgentOptions settings = options.Value;

    // エージェント名と指示
    public override string    Name         => "Agent";
    protected override string Instructions => """
        あなたはグループチャットに参加している AI エージェント「Agent」です。
        参加者の発言は「名前: 本文」の形式で届きます。発言者を区別して応答してください。
        必要に応じて Web 検索ツールを使い、日本語で簡潔に答えてください。
        """;

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

    // MCP サーバーのツールを取得
    // - McpClient に接続し、ツール一覧を取得して返す
    protected override async Task<(IEnumerable<McpClient>, IEnumerable<McpClientTool>)> GetMcpToolsAsync()
    {
        IClientTransport clientTransport = GetBraveSearchToolClientTransport();
        McpClient client = await McpClient.CreateAsync(clientTransport);
        IList<McpClientTool> tools = await client.ListToolsAsync();
        foreach (var tool in tools)
            Logger.LogDebug("MCP tool: {ToolName} ({ToolDescription})", tool.Name, tool.Description);

        return ([client], tools);

        //// Playwright ツールを使うためのクライアント生成
        //static IClientTransport GetPlaywrightToolClientTransport()
        //    => new StdioClientTransport(new() {
        //        Name      = "playwright-mcp",
        //        Command   = "npx"           ,
        //        Arguments = ["@playwright/mcp@latest"]
        //    });

        // BraveSearch ツールを使うためのクライアント生成
        // - npx -y で取得されるパッケージはバージョンを固定する (サプライチェーン対策)
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

    // セキュリティ上、エンドポイントや API キーはソースコードに書かず、構成 (User Secrets / 環境変数など) から取得する
    static string GetRequired(string value, string environmentVariable)
        => string.IsNullOrEmpty(value)
           ? throw new InvalidOperationException($"Please set the {environmentVariable} environment variable (or {ChatAgentOptions.SectionName} configuration).")
           : value;
}
