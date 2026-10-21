using Microsoft.Extensions.AI;

namespace FCAIChat.Tests.TestDoubles;

/// <summary>テスト用の IChatClient (LLM を呼ばずに決まった応答を返す)</summary>
/// <remarks>受け取ったメッセージを記録するので、会話の履歴が渡されているかを確認できる。</remarks>
/// <param name="getResponseText">受け取ったメッセージから応答の本文を作る関数 (省略時は "応答")</param>
public sealed class FakeChatClient(Func<IReadOnlyList<ChatMessage>, string>? getResponseText = null) : IChatClient
{
    readonly Func<IReadOnlyList<ChatMessage>, string> getResponseText = getResponseText ?? (_ => "応答");

    /// <summary>呼び出しごとに受け取ったメッセージ (古い順)</summary>
    public List<IReadOnlyList<ChatMessage>> ReceivedMessages { get; } = [];

    /// <inheritdoc/>
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ChatMessage> messageList = [.. messages];
        ReceivedMessages.Add(messageList);
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, getResponseText(messageList))));
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var update in response.ToChatResponseUpdates())
            yield return update;
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? serviceKey = null)
        => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    /// <inheritdoc/>
    public void Dispose() {}
}
