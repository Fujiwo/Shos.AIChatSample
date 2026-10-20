using FCAIChat.AIAgents;
using FCAIChat.Data;
using FCAIChat.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace FCAIChat.Hubs;

/// <summary>グループチャットの SignalR ハブ</summary>
/// <remarks>
/// <para>クライアント (wwwroot/js/chat.js) から呼ばれるメソッド: <see cref="SendMessage"/>, <see cref="Clear"/></para>
/// <para>クライアントへ送る通知:</para>
/// <list type="bullet">
/// <item><c>ReceiveMessage(id, userName, content, createdAt)</c> - 新しいメッセージ (createdAt は UTC の ISO-8601 文字列)</item>
/// <item><c>Cleared()</c> - チャットログが削除された</item>
/// </list>
/// <para>ハブのインスタンスはメソッド呼び出しごとに生成されるので、状態は DB / IThreadStore / シングルトンの ChatAgent に持たせる。</para>
/// </remarks>
/// <param name="dbContext">チャットメッセージの DB</param>
/// <param name="threadStore">ユーザーごとのエージェントとの会話の保存先</param>
/// <param name="chatAgent">AI エージェント</param>
[Authorize] // ログインしたユーザーだけが接続できる
public class ChatHub(MessagesDbContext dbContext, IThreadStore threadStore, ChatAgent chatAgent) : Hub
{
    /// <summary>1 メッセージあたりの最大文字数 (入力欄の maxlength にも使う)</summary>
    public const int MaxPromptLength = 4000;

    /// <summary>チャットログ (全員共有) と、呼び出し元ユーザーのエージェントとの会話を削除する</summary>
    /// <remarks>他のユーザーのエージェントとの会話は削除しない。</remarks>
    public async Task Clear()
    {
        await threadStore.DeleteThreadAsync(Context.UserId);
        // 全行を読み込まずに、DELETE 文 1 回で削除する
        await dbContext.Messages.ExecuteDeleteAsync();
        // 全クライアントの画面をクリアさせる
        await Clients.All.SendAsync("Cleared");
    }

    /// <summary>メッセージを送信する</summary>
    /// <param name="prompt">メッセージ本文。"@Agent" を含む場合は AI エージェントが応答する</param>
    /// <remarks>
    /// 処理の流れ:
    /// <list type="number">
    /// <item>送信者のメッセージを DB に保存し、全員に配信する</item>
    /// <item>"@Agent" 宛てであれば、送信者とエージェントとの会話を復元して問い合わせる</item>
    /// <item>更新された会話を保存し、エージェントの応答を DB に保存して全員に配信する</item>
    /// </list>
    /// エージェントの応答を待つ間、同じクライアントからの次の呼び出しは待たされる (SignalR の既定動作)。
    /// </remarks>
    /// <exception cref="HubException">メッセージが長すぎる場合、または未認証の場合</exception>
    public async Task SendMessage(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return;
        if (prompt.Length > MaxPromptLength)
            throw new HubException($"メッセージは {MaxPromptLength} 文字以内で入力してください。");

        // 送信者名はクライアントから受け取らず、認証済みユーザーから決める (なりすまし防止)
        var user = Context.UserName;
        await AddAndBroadcastAsync(user, prompt);

        var (isForAgent, promptForAgent) = GetPromptForAgent(chatAgent.Name, prompt);
        if (isForAgent) {
            // 会話はユーザーごとに保存する (ConnectionId は再接続のたびに変わるため)
            var userId              = Context.UserId;
            // グループチャットなので、誰の発言かが分かるように「名前: 本文」の形式で渡す
            var (response, session) = await chatAgent.GetResponseAsync($"{user}: {promptForAgent}", await threadStore.GetThreadAsync(userId));
            // 失敗時 (session が null) は、前回までの会話をそのまま残す
            if (session.HasValue)
                await threadStore.SaveThreadAsync(userId, session.Value);

            await AddAndBroadcastAsync(chatAgent.Name, response);
        }

        // メッセージを DB に保存し、全クライアントに配信する
        // - ID は DB への保存時に採番されるので、保存してから配信する (クライアントはこの ID で表示用の HTML を取得する)
        async Task AddAndBroadcastAsync(string user, string content)
        {
            var message = new Message { UserName = user, Content = content, CreatedAt = DateTime.UtcNow };
            dbContext.Messages.Add(message);
            await dbContext.SaveChangesAsync();
            await Clients.All.SendAsync("ReceiveMessage", message.Id, message.UserName, message.Content, message.CreatedAt.ToString("o"));
        }
    }

    /// <summary>プロンプトが "@エージェント名" 宛てかを判定し、宛てであればメンションを取り除いたプロンプトを返す</summary>
    /// <param name="agentName">エージェント名</param>
    /// <param name="prompt">メッセージ本文</param>
    /// <returns>エージェント宛てかどうかと、メンションを取り除いたプロンプト (宛てでなければ空文字)</returns>
    /// <remarks>
    /// 大文字小文字は区別しない。"@Agentic" や "foo@Agent.com" のような英単語・アドレスの一部には反応しない。
    /// "こんにちは@Agent" や "@Agentさん" のように日本語と続けて書いた場合は反応する (\w は日本語も含むので使わない)。
    /// </remarks>
    /// <example>
    /// <c>GetPromptForAgent("Agent", "@agent 今日の天気は?")</c> → <c>(true, "今日の天気は?")</c>
    /// </example>
    public static (bool isForAgent, string prompt) GetPromptForAgent(string agentName, string prompt)
    {
        // (?<!...) 直前が英数字・@・. でない / (?!...) 直後が英数字でない
        var mention = new Regex($@"(?<![A-Za-z0-9_@.])@{Regex.Escape(agentName)}(?![A-Za-z0-9_])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return mention.IsMatch(prompt)
               ? (true, mention.Replace(prompt, "").Trim())
               : (false, string.Empty);
    }
}
