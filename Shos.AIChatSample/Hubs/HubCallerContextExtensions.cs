using Microsoft.AspNetCore.SignalR;

namespace FCAIChat.Hubs;

/// <summary>HubCallerContext (ハブのメソッドを呼び出した接続の情報) の拡張プロパティ</summary>
/// <remarks>
/// C# 14 の拡張メンバー (extension ブロック) で定義しているので、ハブの中では
/// <c>Context.UserId</c> / <c>Context.UserName</c> のように通常のプロパティと同じ書き方で使える。
/// </remarks>
public static class HubCallerContextExtensions
{
    extension(HubCallerContext context)
    {
        /// <summary>認証済みユーザーの ID (Identity のユーザー ID)</summary>
        /// <exception cref="HubException">未認証の場合</exception>
        public string UserId   => context.UserIdentifier       ?? throw new HubException("Unauthorized");
        /// <summary>認証済みユーザーの名前 (既定の Identity ではメールアドレス)</summary>
        /// <exception cref="HubException">未認証の場合</exception>
        public string UserName => context.User?.Identity?.Name ?? throw new HubException("Unauthorized");
    }
}
