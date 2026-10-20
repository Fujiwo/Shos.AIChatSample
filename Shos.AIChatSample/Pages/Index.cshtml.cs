using FCAIChat.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FCAIChat.Pages;

/// <summary>チャット画面 (トップページ)</summary>
/// <remarks>
/// 初期表示ではこれまでのメッセージを DB から読み込んで表示する。
/// 以降のメッセージは SignalR (wwwroot/js/chat.js) で受信して追加する。
/// </remarks>
/// <param name="dbContext">チャットメッセージの DB</param>
[Authorize] // 未ログインの場合はログイン画面にリダイレクトされる
public class IndexModel(MessagesDbContext dbContext) : PageModel
{
    // 初期表示するメッセージの最大件数 (新しいものから)
    const int maxMessageCount = 200;

    /// <summary>表示するメッセージ (古い順)</summary>
    public IReadOnlyList<Message> Messages { get; private set; } = [];

    /// <summary>GET 時の処理: 最新のメッセージを読み込む</summary>
    public async Task OnGetAsync()
    {
        // 新しい順に最大件数だけ取得する (表示するだけなので変更追跡はしない)
        var latestMessages = await dbContext.Messages.AsNoTracking()
                                                     .OrderByDescending(message => message.CreatedAt)
                                                     .ThenByDescending (message => message.Id       )
                                                     .Take(maxMessageCount)
                                                     .ToListAsync();
        // 古い順に並べ替えて表示する
        // (AsEnumerable() を挟むのは、List<T>.Reverse() (戻り値なし) ではなく Enumerable.Reverse() を使うため)
        Messages = [.. latestMessages.AsEnumerable().Reverse()];
    }
}
