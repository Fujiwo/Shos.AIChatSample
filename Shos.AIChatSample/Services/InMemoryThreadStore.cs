using System.Collections.Concurrent;
using System.Text.Json;

namespace FCAIChat.Services;

/// <summary><see cref="IThreadStore"/> のインメモリ実装</summary>
/// <remarks>
/// ユーザー単位で保存するので、エントリ数はユーザー数が上限になる。
/// アプリを再起動すると内容は消える。複数のサーバーで動かす場合は共有できないので、DB などを使う実装に差し替えること。
/// </remarks>
public class InMemoryThreadStore : IThreadStore
{
    // キー: ユーザー ID / 値: 会話の JSON 文字列
    // - JsonElement は元の JsonDocument に依存するので、文字列にして保持する
    // - 複数の接続から同時にアクセスされるので、スレッドセーフな ConcurrentDictionary を使う
    readonly ConcurrentDictionary<string, string> threadStorage = new();

    /// <inheritdoc/>
    public Task SaveThreadAsync(string userId, JsonElement serializedThread)
    {
        threadStorage[userId] = serializedThread.GetRawText();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<JsonElement?> GetThreadAsync(string userId)
        => Task.FromResult(threadStorage.TryGetValue(userId, out var json)
                           ? JsonSerializer.Deserialize<JsonElement>(json)
                           : (JsonElement?)null);

    /// <inheritdoc/>
    public Task DeleteThreadAsync(string userId)
    {
        threadStorage.TryRemove(userId, out _);
        return Task.CompletedTask;
    }
}
