using System.Text.Json;

namespace FCAIChat.Services;

/// <summary>Interface for storing and retrieving AgentSession data</summary>
public interface IThreadStore
{
    /// <summary>Saves a serialized thread for a specific user</summary>
    /// <param name="userId">The user identifier (Hub Context.UserIdentifier)</param>
    /// <param name="serializedThread">The serialized thread as JsonElement</param>
    Task SaveThreadAsync(string userId, JsonElement serializedThread);

    /// <summary>Retrieves a serialized thread for a specific user</summary>
    /// <param name="userId">The user identifier (Hub Context.UserIdentifier)</param>
    /// <returns>The serialized thread as JsonElement, or null if not found</returns>
    Task<JsonElement?> GetThreadAsync(string userId);

    /// <summary>Deletes a stored thread for a specific user</summary>
    /// <param name="userId">The user identifier (Hub Context.UserIdentifier)</param>
    Task DeleteThreadAsync(string userId);
}
