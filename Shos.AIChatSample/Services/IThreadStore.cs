using System.Text.Json;

namespace FCAIChat.Services;

/// <summary>AI エージェントとの会話 (シリアライズした AgentSession) の保存先</summary>
/// <remarks>
/// ユーザーごとに 1 つの会話を保存する。チャットログ (Messages テーブル) とは別物で、
/// エージェントが文脈 (前回までのやり取り) を覚えておくために使う。
/// 永続化したい場合は、DB や分散キャッシュを使う実装に差し替える。
/// </remarks>
public interface IThreadStore
{
    /// <summary>ユーザーの会話を保存する (既にあれば上書きする)</summary>
    /// <param name="userId">ユーザー ID (ハブの Context.UserIdentifier)</param>
    /// <param name="serializedThread">シリアライズした会話</param>
    Task SaveThreadAsync(string userId, JsonElement serializedThread);

    /// <summary>ユーザーの会話を取得する</summary>
    /// <param name="userId">ユーザー ID (ハブの Context.UserIdentifier)</param>
    /// <returns>シリアライズした会話。保存されていなければ null</returns>
    Task<JsonElement?> GetThreadAsync(string userId);

    /// <summary>ユーザーの会話を削除する (保存されていなければ何もしない)</summary>
    /// <param name="userId">ユーザー ID (ハブの Context.UserIdentifier)</param>
    Task DeleteThreadAsync(string userId);
}
