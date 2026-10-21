using FCAIChat.Hubs;
using FCAIChat.Tests.TestDoubles;
using Microsoft.AspNetCore.SignalR;

namespace FCAIChat.Tests.Hubs;

/// <summary><see cref="HubCallerContextExtensions"/> のテスト</summary>
public class HubCallerContextExtensionsTests
{
    [Fact]
    public void 認証済みならユーザーIDとユーザー名を返す()
    {
        var context = new FakeHubCallerContext(userId: "user-1", userName: "alice@example.com");

        Assert.Equal("user-1"           , context.UserId  );
        Assert.Equal("alice@example.com", context.UserName);
    }

    [Fact]
    public void ユーザーIDがなければHubExceptionを投げる()
    {
        var context = new FakeHubCallerContext(userId: null, userName: "alice@example.com");

        var exception = Assert.Throws<HubException>(() => context.UserId);
        Assert.Equal("Unauthorized", exception.Message);
    }

    [Fact]
    public void ユーザー名がなければHubExceptionを投げる()
    {
        var context = new FakeHubCallerContext(userId: "user-1", userName: null);

        var exception = Assert.Throws<HubException>(() => context.UserName);
        Assert.Equal("Unauthorized", exception.Message);
    }
}
