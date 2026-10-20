using FCAIChat.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FCAIChat.Pages.Messages;

/// <summary>1 件のメッセージを部分ビュー (_MessagePartial) としてレンダリングして返すページ</summary>
/// <remarks>
/// <para>
/// SignalR でメッセージを受信したクライアント (wwwroot/js/chat.js) が
/// <c>GET /Messages/RenderPartial?id=123</c> で呼び出し、返ってきた HTML をチャット欄に追加する。
/// サーバー側でレンダリングするので、初期表示 (Index) と同じ見た目になる。
/// </para>
/// <para>本文をクエリ文字列で受け取ると URL 長の上限を超えるため、ID を受け取って DB から読み込む。</para>
/// </remarks>
/// <param name="dbContext">チャットメッセージの DB</param>
[Authorize]
public class RenderPartialModel(MessagesDbContext dbContext) : PageModel
{
    /// <summary>表示するメッセージ</summary>
    public Message? Message { get; private set; }

    /// <summary>GET 時の処理: 指定された ID のメッセージを読み込む</summary>
    /// <param name="id">メッセージの ID</param>
    /// <returns>メッセージの HTML。見つからない場合 (削除済みなど) は 404</returns>
    public async Task<IActionResult> OnGetAsync(int id)
    {
        Message = await dbContext.Messages.AsNoTracking()
                                          .FirstOrDefaultAsync(message => message.Id == id);
        return Message is null ? NotFound() : Page();
    }
}
