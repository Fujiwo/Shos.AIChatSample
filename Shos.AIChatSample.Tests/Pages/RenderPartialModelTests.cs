using FCAIChat.Data;
using FCAIChat.Pages.Messages;
using FCAIChat.Tests.TestDoubles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FCAIChat.Tests.Pages;

/// <summary><see cref="RenderPartialModel"/> (メッセージ 1 件の HTML 断片) のテスト</summary>
public sealed class RenderPartialModelTests : IDisposable
{
    readonly SqliteMessagesDb db = new();

    public void Dispose() => db.Dispose();

    [Fact]
    public async Task 指定したIDのメッセージを読み込んでページを返す()
    {
        var added = db.AddMessages(new Message { UserName = "alice", Content = "1" },
                                   new Message { UserName = "bob"  , Content = "2" });
        using var dbContext = db.CreateDbContext();
        var model = new RenderPartialModel(dbContext);

        var result = await model.OnGetAsync(added[1].Id);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.Message);
        Assert.Equal(added[1].Id, model.Message.Id      );
        Assert.Equal("bob"      , model.Message.UserName);
        Assert.Equal("2"        , model.Message.Content );
        Assert.Equal(DateTimeKind.Utc, model.Message.CreatedAt.Kind);
    }

    [Theory]
    [InlineData( 0)]
    [InlineData(-1)]
    [InlineData(999)]
    public async Task メッセージが見つからなければ404を返す(int id)
    {
        db.AddMessages(new Message { UserName = "alice", Content = "1" });
        using var dbContext = db.CreateDbContext();
        var model = new RenderPartialModel(dbContext);

        var result = await model.OnGetAsync(id);

        Assert.IsType<NotFoundResult>(result);
        Assert.Null(model.Message);
    }

    [Fact]
    public async Task 削除済みのメッセージなら404を返す()
    {
        var id = db.AddMessages(new Message { UserName = "alice", Content = "1" })[0].Id;
        using (var dbContext = db.CreateDbContext()) {
            dbContext.Messages.RemoveRange(dbContext.Messages);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var readDbContext = db.CreateDbContext();
        var result = await new RenderPartialModel(readDbContext).OnGetAsync(id);

        Assert.IsType<NotFoundResult>(result);
    }
}
