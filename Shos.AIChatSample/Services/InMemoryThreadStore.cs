using System.Collections.Concurrent;
using System.Text.Json;

namespace FCAIChat.Services;

/// <summary>In-memory implementation of IThreadStore using JSON storage</summary>
/// <remarks>Keyed by user, so the number of entries is bounded by the number of users. Contents are lost on restart.</remarks>
public class InMemoryThreadStore : IThreadStore
{
    readonly ConcurrentDictionary<string, string> threadStorage = new();

    public Task SaveThreadAsync(string userId, JsonElement serializedThread)
    {
        threadStorage[userId] = serializedThread.GetRawText();
        return Task.CompletedTask;
    }

    public Task<JsonElement?> GetThreadAsync(string userId)
        => Task.FromResult(threadStorage.TryGetValue(userId, out var json)
                           ? JsonSerializer.Deserialize<JsonElement>(json)
                           : (JsonElement?)null);

    public Task DeleteThreadAsync(string userId)
    {
        threadStorage.TryRemove(userId, out _);
        return Task.CompletedTask;
    }
}
