using FCAIChat.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FCAIChat.Pages
{
    [Authorize]
    public class IndexModel : PageModel
    {
        // 初期表示するメッセージの最大件数 (新しいものから)
        const int maxMessageCount = 200;

        readonly MessagesDbContext dbContext;

        public IReadOnlyList<Message> Messages { get; private set; } = [];

        public IndexModel(MessagesDbContext dbContext) => this.dbContext = dbContext;

        public async Task OnGetAsync()
            => Messages = (await dbContext.Messages.AsNoTracking()
                                                   .OrderByDescending(message => message.CreatedAt)
                                                   .ThenByDescending (message => message.Id       )
                                                   .Take(maxMessageCount)
                                                   .ToListAsync())
                          .AsEnumerable()
                          .Reverse()
                          .ToList();
    }
}
