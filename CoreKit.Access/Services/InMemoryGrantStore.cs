using System.Collections.Concurrent;
using CoreKit.Access.Contracts;

namespace CoreKit.Access.Services;

internal sealed class InMemoryGrantStore : IGrantStore
{
    private readonly ConcurrentDictionary<Guid, DateTime> _consumed = new();
    private readonly TimeProvider _time;

    public InMemoryGrantStore(TimeProvider time) => _time = time;

    public Task<bool> TryConsumeAsync(Guid grantId, DateTime expiresAtUtc, CancellationToken ct = default)
    {
        Prune();
        return Task.FromResult(_consumed.TryAdd(grantId, expiresAtUtc));
    }

    private void Prune()
    {
        var now = _time.GetUtcNow().UtcDateTime;
        foreach (var kvp in _consumed)
            if (kvp.Value < now) _consumed.TryRemove(kvp.Key, out _);
    }
}