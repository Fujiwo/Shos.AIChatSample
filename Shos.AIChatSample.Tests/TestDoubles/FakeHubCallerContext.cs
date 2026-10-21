using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace FCAIChat.Tests.TestDoubles;

/// <summary>テスト用の HubCallerContext (ハブのメソッドを呼び出した接続の情報)</summary>
/// <param name="userId">ユーザー ID (null なら未認証)</param>
/// <param name="userName">ユーザー名 (null なら未認証)</param>
public sealed class FakeHubCallerContext(string? userId, string? userName) : HubCallerContext
{
    /// <inheritdoc/>
    public override string                    ConnectionId        { get; } = Guid.NewGuid().ToString();
    /// <inheritdoc/>
    public override string?                   UserIdentifier      { get; } = userId;
    /// <inheritdoc/>
    public override ClaimsPrincipal?          User                { get; } = CreateUser(userName);
    /// <inheritdoc/>
    public override IDictionary<object, object?> Items            { get; } = new Dictionary<object, object?>();
    /// <inheritdoc/>
    public override IFeatureCollection        Features            { get; } = new FeatureCollection();
    /// <inheritdoc/>
    public override CancellationToken         ConnectionAborted   => CancellationToken.None;

    /// <inheritdoc/>
    public override void Abort() {}

    // userName が null なら未認証のユーザー、そうでなければ認証済みのユーザーを作る
    static ClaimsPrincipal CreateUser(string? userName)
        => userName is null
           ? new ClaimsPrincipal(new ClaimsIdentity())
           : new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, userName)], authenticationType: "Test"));
}
