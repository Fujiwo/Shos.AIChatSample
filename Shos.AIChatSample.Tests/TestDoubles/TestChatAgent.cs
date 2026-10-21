using FCAIChat.AIAgents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace FCAIChat.Tests.TestDoubles;

/// <summary>テスト用の AI エージェント (Azure OpenAI / MCP サーバーを使わない)</summary>
/// <param name="chatClient">使用するチャットクライアント (省略時は <see cref="FakeChatClient"/>)</param>
public sealed class TestChatAgent(IChatClient? chatClient = null) : ChatAgent(NullLogger.Instance)
{
    /// <summary>使用するチャットクライアント</summary>
    public IChatClient ChatClient          { get; } = chatClient ?? new FakeChatClient();
    /// <summary>true にすると、チャットクライアントの生成時に例外を投げる (設定漏れの再現用)</summary>
    public bool        ThrowOnGetClient    { get; set; }
    /// <summary>チャットクライアントを生成した回数</summary>
    public int         GetChatClientCount  { get; private set; }

    /// <inheritdoc/>
    public override string    Name         => "Agent";
    /// <inheritdoc/>
    protected override string Instructions => "テスト用のエージェントです。";

    /// <inheritdoc/>
    protected override IChatClient GetChatClient()
    {
        GetChatClientCount++;
        return ThrowOnGetClient ? throw new InvalidOperationException("Please set the AZURE_OPENAI_ENDPOINT environment variable.") : ChatClient;
    }
}
