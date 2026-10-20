using FCAIChat.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FCAIChat.Pages.Messages
{
    // 1 件のメッセージを部分ビューとしてレンダリングして返す
    // - 本文をクエリ文字列で受け取ると URL 長の上限を超えるため、ID を受け取って DB から読み込む
    [Authorize]
    public class RenderPartialModel : PageModel
    {
        readonly MessagesDbContext dbContext;

        public Message? Message { get; private set; }

        public RenderPartialModel(MessagesDbContext dbContext) => this.dbContext = dbContext;

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Message = await dbContext.Messages.AsNoTracking()
                                              .FirstOrDefaultAsync(message => message.Id == id);
            return Message is null ? NotFound() : Page();
        }
    }
}
