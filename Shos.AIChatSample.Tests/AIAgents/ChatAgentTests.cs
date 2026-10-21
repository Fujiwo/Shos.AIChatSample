using FCAIChat.Tests.TestDoubles;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace FCAIChat.Tests.AIAgents;

/// <summary><see cref="FCAIChat.AIAgents.ChatAgent"/> (基底クラスの共通処理) のテスト</summary>
public class ChatAgentTests
{
    const string errorResponse = "(エラーが発生したため応答できませんでした)";

    [Fact]
    public async Task GetResponseAsync_チャットクライアントの応答と会話を返す()
    {
        var chatClient = new FakeChatClient(_ => "こんにちは!");
        await using var agent = new TestChatAgent(chatClient);

        var (response, session) = await agent.GetResponseAsync("こんにちは", null);

        Assert.Equal  ("こんにちは!", response);
        Assert.NotNull(session);
        var prompt = Assert.Single(chatClient.ReceivedMessages)[^1];
        Assert.Equal(ChatRole.User, prompt.Role);
        Assert.Equal("こんにちは"  , prompt.Text);
    }

    [Fact]
    public async Task GetResponseAsync_返された会話を渡すと前回までの履歴が引き継がれる()
    {
        var chatClient = new FakeChatClient(messages => $"応答{messages.Count(message => message.Role == ChatRole.User)}");
        await using var agent = new TestChatAgent(chatClient);

        var (response1, session1) = await agent.GetResponseAsync("1 回目", null    );
        var (response2, _       ) = await agent.GetResponseAsync("2 回目", session1);

        Assert.Equal("応答1", response1);
        Assert.Equal("応答2", response2);
        var texts = chatClient.ReceivedMessages[1].Select(message => message.Text).ToList();
        Assert.Equal(["1 回目", "応答1", "2 回目"], texts);
    }

    [Fact]
    public async Task GetResponseAsync_会話を渡さなければ新しい会話になる()
    {
        var chatClient = new FakeChatClient();
        await using var agent = new TestChatAgent(chatClient);

        await agent.GetResponseAsync("1 回目", null);
        await agent.GetResponseAsync("2 回目", null);

        Assert.Equal(["2 回目"], chatClient.ReceivedMessages[1].Select(message => message.Text));
    }

    [Fact]
    public async Task GetResponseAsync_会話は文字列に戻してから復元しても引き継がれる()
    {
        // InMemoryThreadStore と同じく、JSON 文字列にして保存した会話を復元できること
        var chatClient = new FakeChatClient();
        await using var agent = new TestChatAgent(chatClient);

        var (_, session) = await agent.GetResponseAsync("1 回目", null);
        var restored     = JsonSerializer.Deserialize<JsonElement>(session!.Value.GetRawText());
        await agent.GetResponseAsync("2 回目", restored);

        Assert.Contains("1 回目", chatClient.ReceivedMessages[1].Select(message => message.Text));
    }

    [Fact]
    public async Task GetResponseAsync_会話を復元できなければ新しい会話として応答する()
    {
        var chatClient = new FakeChatClient();
        await using var agent = new TestChatAgent(chatClient);
        var brokenSession = JsonSerializer.SerializeToElement(12345);

        var (response, session) = await agent.GetResponseAsync("こんにちは", brokenSession);

        Assert.Equal  ("応答", response);
        Assert.NotNull(session);
        Assert.Equal(["こんにちは"], Assert.Single(chatClient.ReceivedMessages).Select(message => message.Text));
    }

    [Fact]
    public async Task GetResponseAsync_例外が発生したらエラーメッセージを返し_会話はnullになる()
    {
        await using var agent = new TestChatAgent { ThrowOnGetClient = true };

        var (response, session) = await agent.GetResponseAsync("こんにちは", null);

        Assert.Equal(errorResponse, response);
        Assert.Null (session);
    }

    [Fact]
    public async Task GetResponseAsync_チャットクライアントの例外も応答として返す()
    {
        await using var agent = new TestChatAgent(new FakeChatClient(_ => throw new HttpRequestException("接続できません")));

        var (response, session) = await agent.GetResponseAsync("こんにちは", null);

        Assert.Equal(errorResponse, response);
        Assert.Null (session);
    }

    [Fact]
    public async Task GetResponseAsync_AIAgentは初回だけ生成して使い回す()
    {
        await using var agent = new TestChatAgent();

        await agent.GetResponseAsync("1 回目", null);
        await agent.GetResponseAsync("2 回目", null);

        Assert.Equal(1, agent.GetChatClientCount);
    }

    [Fact]
    public async Task GetResponseAsync_同時に呼ばれてもAIAgentは1回だけ生成する()
    {
        await using var agent = new TestChatAgent();

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(index => Task.Run(() => agent.GetResponseAsync($"{index} 回目", null))));

        Assert.All(results, result => Assert.Equal("応答", result.response));
        Assert.Equal(1, agent.GetChatClientCount);
    }

    [Fact]
    public async Task GetResponseAsync_生成に失敗したら次の呼び出しで再試行する()
    {
        await using var agent = new TestChatAgent { ThrowOnGetClient = true };

        var (response1, _) = await agent.GetResponseAsync("1 回目", null);
        agent.ThrowOnGetClient = false;
        var (response2, _) = await agent.GetResponseAsync("2 回目", null);

        Assert.Equal(errorResponse, response1);
        Assert.Equal("応答"       , response2);
        Assert.Equal(2, agent.GetChatClientCount);
    }

    [Fact]
    public async Task DisposeAsync_一度も使っていなくても失敗しない()
    {
        var agent = new TestChatAgent();

        await agent.DisposeAsync();
    }
}
