using FCAIChat.Data;
using FCAIChat.Pages;
using FCAIChat.Tests.TestDoubles;

namespace FCAIChat.Tests.Pages;

/// <summary><see cref="IndexModel"/> (チャット画面の初期表示) のテスト</summary>
public sealed class IndexModelTests : IDisposable
{
    // IndexModel で初期表示するメッセージの最大件数
    const int maxMessageCount = 200;

    readonly SqliteMessagesDb db = new();

    public void Dispose() => db.Dispose();

    async Task<IReadOnlyList<Message>> GetMessagesAsync()
    {
        using var dbContext = db.CreateDbContext();
        var model = new IndexModel(dbContext);
        await model.OnGetAsync();
        return model.Messages;
    }

    [Fact]
    public async Task メッセージがなければ空()
        => Assert.Empty(await GetMessagesAsync());

    [Fact]
    public async Task メッセージを古い順に返す()
    {
        var baseTime = new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc);
        db.AddMessages(new Message { Content = "2", CreatedAt = baseTime.AddMinutes(2) },
                       new Message { Content = "1", CreatedAt = baseTime.AddMinutes(1) },
                       new Message { Content = "3", CreatedAt = baseTime.AddMinutes(3) });

        var messages = await GetMessagesAsync();

        Assert.Equal(["1", "2", "3"], messages.Select(message => message.Content));
    }

    [Fact]
    public async Task 日時が同じならIDの順に返す()
    {
        var createdAt = new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc);
        var added     = db.AddMessages([.. Enumerable.Range(1, 5).Select(index => new Message { Content = $"{index}", CreatedAt = createdAt })]);

        var messages = await GetMessagesAsync();

        Assert.Equal(added.Select(message => message.Id), messages.Select(message => message.Id));
    }

    [Fact]
    public async Task 最大件数を超える場合は新しいものだけを古い順に返す()
    {
        var baseTime = new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc);
        db.AddMessages([.. Enumerable.Range(1, maxMessageCount + 10).Select(index => new Message { Content = $"{index}", CreatedAt = baseTime.AddSeconds(index) })]);

        var messages = await GetMessagesAsync();

        Assert.Equal(maxMessageCount, messages.Count);
        Assert.Equal("11"                           , messages[ 0].Content);
        Assert.Equal($"{maxMessageCount + 10}"      , messages[^1].Content);
    }

    [Fact]
    public async Task 読み出したCreatedAtはUTCになる()
    {
        db.AddMessages(new Message { Content = "1" });

        var message = Assert.Single(await GetMessagesAsync());

        Assert.Equal(DateTimeKind.Utc, message.CreatedAt.Kind);
    }
}
