using FCAIChat.Data;
using FCAIChat.Tests.TestDoubles;

namespace FCAIChat.Tests.Data;

/// <summary><see cref="MessagesDbContext"/> のテスト</summary>
public sealed class MessagesDbContextTests : IDisposable
{
    readonly SqliteMessagesDb db = new();

    public void Dispose() => db.Dispose();

    [Fact]
    public void Messageの既定値()
    {
        var before  = DateTime.UtcNow;
        var message = new Message();

        Assert.Equal(0           , message.Id      );
        Assert.Equal(string.Empty, message.Content );
        Assert.Equal(string.Empty, message.UserName);
        Assert.Equal(DateTimeKind.Utc, message.CreatedAt.Kind);
        Assert.InRange(message.CreatedAt, before, DateTime.UtcNow);
    }

    [Fact]
    public void 保存するとIDが採番される()
    {
        var messages = db.AddMessages(new Message { UserName = "alice", Content = "1" },
                                      new Message { UserName = "bob"  , Content = "2" });

        Assert.All(messages, message => Assert.True(message.Id > 0));
        Assert.NotEqual(messages[0].Id, messages[1].Id);
    }

    [Fact]
    public void 読み出したCreatedAtはUTCになる()
    {
        var createdAt = new DateTime(2026, 10, 21, 1, 2, 3, DateTimeKind.Utc);
        var id        = db.AddMessages(new Message { UserName = "alice", Content = "1", CreatedAt = createdAt })[0].Id;

        // 変更追跡のキャッシュを使わないように、新しい DbContext で読み直す
        using var dbContext = db.CreateDbContext();
        var message = dbContext.Messages.Single(message => message.Id == id);

        Assert.Equal(DateTimeKind.Utc, message.CreatedAt.Kind);
        Assert.Equal(createdAt       , message.CreatedAt     );
        Assert.Equal("2026-10-21T01:02:03.0000000Z", message.CreatedAt.ToString("o"));
    }

    [Fact]
    public void 本文と送信者名をそのまま保存する()
    {
        const string content = "<script>alert('x')</script>\n2 行目 😀";
        var id = db.AddMessages(new Message { UserName = "alice@example.com", Content = content })[0].Id;

        using var dbContext = db.CreateDbContext();
        var message = dbContext.Messages.Single(message => message.Id == id);

        Assert.Equal("alice@example.com", message.UserName);
        Assert.Equal(content            , message.Content );
    }
}
