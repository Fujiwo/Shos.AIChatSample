using FCAIChat.Data;
using FCAIChat.Hubs;
using FCAIChat.Services;
using FCAIChat.Tests.TestDoubles;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace FCAIChat.Tests.Hubs;

/// <summary><see cref="ChatHub"/> のテスト</summary>
/// <remarks>DB は SQLite のインメモリ DB、AI エージェントは LLM を呼ばない <see cref="TestChatAgent"/> を使う。</remarks>
public sealed class ChatHubTests : IDisposable
{
    const string userId   = "user-1";
    const string userName = "alice@example.com";

    readonly SqliteMessagesDb     db          = new();
    readonly MessagesDbContext    dbContext;
    readonly InMemoryThreadStore  threadStore = new();
    readonly FakeChatClient       chatClient  = new(messages => $"「{messages[^1].Text}」への応答");
    readonly TestChatAgent        chatAgent;
    readonly FakeHubCallerClients clients     = new();

    public ChatHubTests()
    {
        dbContext = db.CreateDbContext();
        chatAgent = new(chatClient);
    }

    public void Dispose()
    {
        dbContext.Dispose();
        db       .Dispose();
    }

    // ログイン済みのユーザーが呼び出すハブを作る
    ChatHub CreateHub(string? userId = userId, string? userName = userName)
        => new(dbContext, threadStore, chatAgent) {
               Context = new FakeHubCallerContext(userId, userName),
               Clients = clients
           };

    // DB に保存されたメッセージ (ID 順)
    List<Message> GetSavedMessages()
    {
        using var dbContext = db.CreateDbContext();
        return [.. dbContext.Messages.OrderBy(message => message.Id)];
    }

    // ---- SendMessage ----

    [Fact]
    public async Task SendMessage_メッセージを保存して全員に配信する()
    {
        var before = DateTime.UtcNow;

        await CreateHub().SendMessage("こんにちは");

        var message = Assert.Single(GetSavedMessages());
        Assert.Equal(userName    , message.UserName);
        Assert.Equal("こんにちは", message.Content );
        Assert.InRange(message.CreatedAt, before, DateTime.UtcNow);

        var sent = Assert.Single(clients.Sent);
        Assert.Equal("ReceiveMessage", sent.Method);
        Assert.Equal<object?>([message.Id, userName, "こんにちは", message.CreatedAt.ToString("o")], sent.Arguments);
    }

    [Fact]
    public async Task SendMessage_配信する日時はUTCのISO8601形式()
    {
        await CreateHub().SendMessage("こんにちは");

        var createdAt = Assert.IsType<string>(Assert.Single(clients.Sent).Arguments[3]);
        Assert.EndsWith("Z", createdAt);
        Assert.Equal(DateTimeKind.Utc, DateTime.Parse(createdAt, null, System.Globalization.DateTimeStyles.RoundtripKind).Kind);
    }

    [Fact]
    public async Task SendMessage_メンションがなければエージェントは応答しない()
    {
        await CreateHub().SendMessage("Agent さん、こんにちは");

        Assert.Single(GetSavedMessages());
        Assert.Empty (chatClient.ReceivedMessages);
        Assert.Null  (await threadStore.GetThreadAsync(userId));
    }

    [Theory]
    [InlineData(""      )]
    [InlineData(" "     )]
    [InlineData("\r\n\t")]
    public async Task SendMessage_空白だけのメッセージは無視する(string prompt)
    {
        await CreateHub().SendMessage(prompt);

        Assert.Empty(GetSavedMessages());
        Assert.Empty(clients.Sent);
    }

    [Fact]
    public async Task SendMessage_最大文字数ちょうどなら送信できる()
    {
        await CreateHub().SendMessage(new string('あ', ChatHub.MaxPromptLength));

        Assert.Equal(ChatHub.MaxPromptLength, Assert.Single(GetSavedMessages()).Content.Length);
    }

    [Fact]
    public async Task SendMessage_最大文字数を超えるとHubExceptionを投げて保存しない()
    {
        var hub = CreateHub();

        await Assert.ThrowsAsync<HubException>(() => hub.SendMessage(new string('あ', ChatHub.MaxPromptLength + 1)));

        Assert.Empty(GetSavedMessages());
        Assert.Empty(clients.Sent);
    }

    [Fact]
    public async Task SendMessage_未認証ならHubExceptionを投げて保存しない()
    {
        var hub = CreateHub(userId: null, userName: null);

        await Assert.ThrowsAsync<HubException>(() => hub.SendMessage("こんにちは"));

        Assert.Empty(GetSavedMessages());
    }

    [Fact]
    public async Task SendMessage_エージェント宛てなら_送信者名付きでメンションを除いたプロンプトを渡す()
    {
        await CreateHub().SendMessage("@Agent 今日の天気は?");

        var received = Assert.Single(chatClient.ReceivedMessages);
        var prompt   = received[^1];
        Assert.Equal(ChatRole.User                      , prompt.Role);
        Assert.Equal($"{userName}: 今日の天気は?"       , prompt.Text);
    }

    [Fact]
    public async Task SendMessage_エージェント宛てなら_ユーザーの発言とエージェントの応答を順に保存して配信する()
    {
        await CreateHub().SendMessage("@Agent 今日の天気は?");

        var messages = GetSavedMessages();
        Assert.Collection(messages,
            message => {
                Assert.Equal(userName             , message.UserName);
                Assert.Equal("@Agent 今日の天気は?", message.Content );
            },
            message => {
                Assert.Equal(chatAgent.Name                              , message.UserName);
                Assert.Equal($"「{userName}: 今日の天気は?」への応答"    , message.Content );
            });

        Assert.Equal(["ReceiveMessage", "ReceiveMessage"], clients.Sent.Select(sent => sent.Method));
        Assert.Equal(messages.Select(message => (object?)message.Id), clients.Sent.Select(sent => sent.Arguments[0]));
        Assert.Equal(messages.Select(message => (object?)message.UserName), clients.Sent.Select(sent => sent.Arguments[1]));
    }

    [Fact]
    public async Task SendMessage_エージェント宛てなら_会話をユーザーごとに保存する()
    {
        await CreateHub().SendMessage("@Agent 今日の天気は?");

        Assert.NotNull(await threadStore.GetThreadAsync(userId));
        Assert.Null   (await threadStore.GetThreadAsync("user-2"));
    }

    [Fact]
    public async Task SendMessage_同じユーザーの2回目の問い合わせには前回までの会話が渡される()
    {
        await CreateHub().SendMessage("@Agent 私の好きな色は青です");
        // ハブはメソッド呼び出しごとに生成されるので、新しいインスタンスで呼び出す
        await CreateHub().SendMessage("@Agent 私の好きな色は?");

        Assert.Equal(2, chatClient.ReceivedMessages.Count);
        var texts = chatClient.ReceivedMessages[1].Select(message => message.Text).ToList();
        Assert.Contains($"{userName}: 私の好きな色は青です"                 , texts);
        Assert.Contains($"「{userName}: 私の好きな色は青です」への応答"      , texts);
        Assert.Equal   ($"{userName}: 私の好きな色は?"                      , texts[^1]);
    }

    [Fact]
    public async Task SendMessage_別のユーザーの会話は渡されない()
    {
        await CreateHub().SendMessage("@Agent 私の好きな色は青です");
        await CreateHub(userId: "user-2", userName: "bob@example.com").SendMessage("@Agent 私の好きな色は?");

        var texts = chatClient.ReceivedMessages[1].Select(message => message.Text).ToList();
        Assert.DoesNotContain(texts, text => text.Contains("青"));
    }

    [Fact]
    public async Task SendMessage_エージェントが失敗したら_エラーメッセージを配信し_前回までの会話を残す()
    {
        var previousThread = JsonSerializer.SerializeToElement(new { marker = "previous" });
        await threadStore.SaveThreadAsync(userId, previousThread);
        chatAgent.ThrowOnGetClient = true;

        await CreateHub().SendMessage("@Agent 今日の天気は?");

        var reply = GetSavedMessages()[^1];
        Assert.Equal(chatAgent.Name                         , reply.UserName);
        Assert.Equal("(エラーが発生したため応答できませんでした)", reply.Content );
        Assert.Equal(2, clients.Sent.Count);

        var thread = await threadStore.GetThreadAsync(userId);
        Assert.Equal(previousThread.GetRawText(), thread?.GetRawText());
    }

    // ---- Clear ----

    [Fact]
    public async Task Clear_全員のメッセージを削除してClearedを配信する()
    {
        db.AddMessages(new Message { UserName = userName         , Content = "1" },
                       new Message { UserName = "bob@example.com", Content = "2" });

        await CreateHub().Clear();

        Assert.Empty(GetSavedMessages());
        var sent = Assert.Single(clients.Sent);
        Assert.Equal("Cleared", sent.Method);
        Assert.Empty(sent.Arguments);
    }

    [Fact]
    public async Task Clear_呼び出し元ユーザーの会話だけを削除する()
    {
        var thread = JsonSerializer.SerializeToElement(new { });
        await threadStore.SaveThreadAsync(userId  , thread);
        await threadStore.SaveThreadAsync("user-2", thread);

        await CreateHub().Clear();

        Assert.Null   (await threadStore.GetThreadAsync(userId  ));
        Assert.NotNull(await threadStore.GetThreadAsync("user-2"));
    }

    [Fact]
    public async Task Clear_メッセージがなくても失敗しない()
    {
        await CreateHub().Clear();

        Assert.Equal("Cleared", Assert.Single(clients.Sent).Method);
    }

    [Fact]
    public async Task Clear_未認証ならHubExceptionを投げて削除しない()
    {
        db.AddMessages(new Message { UserName = userName, Content = "1" });

        await Assert.ThrowsAsync<HubException>(() => CreateHub(userId: null, userName: null).Clear());

        Assert.Single(GetSavedMessages());
        Assert.Empty (clients.Sent);
    }
}
