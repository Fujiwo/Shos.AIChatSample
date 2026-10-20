using FCAIChat.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FCAIChat.Pages;

[Authorize]
public class IndexModel(MessagesDbContext dbContext) : PageModel
{
    // 初期表示するメッセージの最大件数 (新しいものから)
    const int maxMessageCount = 200;

    public IReadOnlyList<Message> Messages { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var latestMessages = await dbContext.Messages.AsNoTracking()
                                                     .OrderByDescending(message => message.CreatedAt)
                                                     .ThenByDescending (message => message.Id       )
                                                     .Take(maxMessageCount)
                                                     .ToListAsync();
        // 古い順に並べ替えて表示する
        Messages = [.. latestMessages.AsEnumerable().Reverse()];
    }
}
