using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;

namespace FCAIChat.Tests.Integration;

/// <summary>未ログインでのアクセスが拒否されることのテスト (アプリ全体を起動する)</summary>
public class AuthorizationTests(AppFactory factory) : IClassFixture<AppFactory>
{
    readonly HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Theory]
    [InlineData("/"                         )]
    [InlineData("/Index"                    )]
    [InlineData("/Messages/RenderPartial?id=1")]
    public async Task 未ログインならログイン画面にリダイレクトする(string url)
    {
        var response = await client.GetAsync(url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Identity/Account/Login", response.Headers.Location?.PathAndQuery);
    }

    [Fact]
    public async Task 未ログインならハブに接続できない()
    {
        // SignalR の接続は negotiate から始まるので、そこで拒否されることを確認する
        var response = await client.PostAsync("/chatHub/negotiate?negotiateVersion=1", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task プライバシーページは未ログインでも表示できる()
    {
        var response = await client.GetAsync("/Privacy", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
