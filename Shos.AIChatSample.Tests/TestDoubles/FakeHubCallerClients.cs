using Microsoft.AspNetCore.SignalR;

namespace FCAIChat.Tests.TestDoubles;

/// <summary>クライアントへ送った通知 1 件</summary>
/// <param name="Method">クライアント側のメソッド名</param>
/// <param name="Arguments">引数</param>
public sealed record SentMessage(string Method, object?[] Arguments);

/// <summary>テスト用の IHubCallerClients (送信した通知を記録する)</summary>
/// <remarks>どの宛先 (All / Caller / Others など) に送っても、同じ <see cref="Sent"/> に記録する。</remarks>
public sealed class FakeHubCallerClients : IHubCallerClients
{
    readonly RecordingClientProxy proxy = new();

    /// <summary>全クライアントへ送った通知 (送信順)</summary>
    public IReadOnlyList<SentMessage> Sent => proxy.Sent;

    /// <inheritdoc/>
    public IClientProxy All                                                      => proxy;
    /// <inheritdoc/>
    public IClientProxy Caller                                                   => proxy;
    /// <inheritdoc/>
    public IClientProxy Others                                                   => proxy;
    /// <inheritdoc/>
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds)   => proxy;
    /// <inheritdoc/>
    public IClientProxy Client(string connectionId)                              => proxy;
    /// <inheritdoc/>
    public IClientProxy Clients(IReadOnlyList<string> connectionIds)             => proxy;
    /// <inheritdoc/>
    public IClientProxy Group(string groupName)                                  => proxy;
    /// <inheritdoc/>
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => proxy;
    /// <inheritdoc/>
    public IClientProxy Groups(IReadOnlyList<string> groupNames)                 => proxy;
    /// <inheritdoc/>
    public IClientProxy OthersInGroup(string groupName)                          => proxy;
    /// <inheritdoc/>
    public IClientProxy User(string userId)                                      => proxy;
    /// <inheritdoc/>
    public IClientProxy Users(IReadOnlyList<string> userIds)                     => proxy;

    sealed class RecordingClientProxy : IClientProxy
    {
        public List<SentMessage> Sent { get; } = [];

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            Sent.Add(new(method, args));
            return Task.CompletedTask;
        }
    }
}
