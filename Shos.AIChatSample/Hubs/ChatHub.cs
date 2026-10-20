namespace FCAIChat.Hubs
{
    using FCAIChat.AIAgents;
    using FCAIChat.Data;
    using FCAIChat.Services;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.EntityFrameworkCore;
    using System.Text.RegularExpressions;

    [Authorize]
    public class ChatHub : Hub
    {
        // 1 メッセージあたりの最大文字数
        public const int MaxPromptLength = 4000;

        readonly MessagesDbContext dbContext;
        readonly IThreadStore      threadStore;
        readonly ChatAgent         chatAgent;

        public ChatHub(MessagesDbContext dbContext, IThreadStore threadStore, ChatAgent chatAgent)
            => (this.dbContext, this.threadStore, this.chatAgent) = (dbContext, threadStore, chatAgent);

        // チャットログ (全員共有) と、呼び出し元ユーザーのエージェントとの会話を削除する
        public async Task Clear()
        {
            await threadStore.DeleteThreadAsync(GetUserId());
            await dbContext.Messages.ExecuteDeleteAsync();
            await Clients.All.SendAsync("Cleared");
        }

        public async Task SendMessage(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return;
            if (prompt.Length > MaxPromptLength)
                throw new HubException($"メッセージは {MaxPromptLength} 文字以内で入力してください。");

            // 送信者名はクライアントから受け取らず、認証済みユーザーから決める (なりすまし防止)
            var user = Context.User?.Identity?.Name ?? throw new HubException("Unauthorized");
            await AddAndBroadcastAsync(user, prompt);

            var (isForAgent, promptForAgent) = GetPromptForAgent(chatAgent.Name, prompt);
            if (isForAgent) {
                // 会話はユーザーごとに保存する (ConnectionId は再接続のたびに変わるため)
                var userId                = GetUserId();
                var (response, session)   = await chatAgent.GetResponseAsync($"{user}: {promptForAgent}", await threadStore.GetThreadAsync(userId));
                if (session.HasValue)
                    await threadStore.SaveThreadAsync(userId, session.Value);

                await AddAndBroadcastAsync(chatAgent.Name, response);
            }

            async Task AddAndBroadcastAsync(string user, string content)
            {
                var message = new Message { UserName = user, Content = content, CreatedAt = DateTime.UtcNow };
                dbContext.Messages.Add(message);
                await dbContext.SaveChangesAsync();
                await Clients.All.SendAsync("ReceiveMessage", message.Id, message.UserName, message.Content, message.CreatedAt.ToString("o"));
            }
        }

        /// <summary>プロンプトが "@エージェント名" 宛てかを判定し、宛てであればメンションを取り除いたプロンプトを返す</summary>
        /// <remarks>
        /// 大文字小文字は区別しない。"@Agentic" や "foo@Agent.com" のような英単語・アドレスの一部には反応しない。
        /// "こんにちは@Agent" や "@Agentさん" のように日本語と続けて書いた場合は反応する (\w は日本語も含むので使わない)。
        /// </remarks>
        public static (bool isForAgent, string prompt) GetPromptForAgent(string agentName, string prompt)
        {
            var mention = new Regex($@"(?<![A-Za-z0-9_@.])@{Regex.Escape(agentName)}(?![A-Za-z0-9_])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return mention.IsMatch(prompt)
                   ? (true, mention.Replace(prompt, "").Trim())
                   : (false, string.Empty);
        }

        string GetUserId() => Context.UserIdentifier ?? throw new HubException("Unauthorized");
    }
}
