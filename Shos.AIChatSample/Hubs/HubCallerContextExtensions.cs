using Microsoft.AspNetCore.SignalR;

namespace FCAIChat.Hubs;

// HubCallerContext の拡張プロパティ (C# 14 の拡張メンバー)
public static class HubCallerContextExtensions
{
    extension(HubCallerContext context)
    {
        // 認証済みユーザーの ID (未認証なら HubException)
        public string UserId   => context.UserIdentifier       ?? throw new HubException("Unauthorized");
        // 認証済みユーザーの名前 (未認証なら HubException)
        public string UserName => context.User?.Identity?.Name ?? throw new HubException("Unauthorized");
    }
}
