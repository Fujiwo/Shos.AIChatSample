using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;

namespace FCAIChat.Pages;

/// <summary>エラーページ (本番環境で未処理の例外が発生したときに表示する)</summary>
/// <remarks>エラーの内容は毎回異なるので、キャッシュさせない。</remarks>
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    /// <summary>リクエスト ID (ログと照合して原因を調べるために表示する)</summary>
    public string? RequestId { get; private set; }

    /// <summary>リクエスト ID を表示するかどうか</summary>
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    /// <summary>GET 時の処理: 現在のリクエストの ID を取得する</summary>
    public void OnGet() => RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
}
