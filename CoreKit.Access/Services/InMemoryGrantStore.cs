using System.Collections.Concurrent;
using CoreKit.Access.Contracts;

namespace CoreKit.Access.Services;

internal sealed class InMemoryGrantStore : IGrantStore
{
    private readonly ConcurrentDictionary<Guid, DateTime> _consumed = new();
    private readonly TimeProvider _time;

    private readonly TimeSpan _pruneInterval = TimeSpan.FromMinutes(1);
    private DateTime _nextPruneUtc = DateTime.MinValue;
    private readonly object _pruneGate = new();

    public InMemoryGrantStore(TimeProvider time) => _time = time;

    public Task<bool> TryConsumeAsync(Guid grantId, DateTime expiresAtUtc, CancellationToken ct = default)
    {
        MaybePrune();
        var capped = expiresAtUtc > DateTime.UtcNow.AddHours(1)
            ? DateTime.UtcNow.AddHours(1)
            : expiresAtUtc;
        return Task.FromResult(_consumed.TryAdd(grantId, capped));
    }

    private void MaybePrune()
    {
        var now = _time.GetUtcNow().UtcDateTime;
        lock (_pruneGate)
        {
            if (now < _nextPruneUtc) return;
            _nextPruneUtc = now + _pruneInterval;
        }

        foreach (var kvp in _consumed)
            if (kvp.Value < now) _consumed.TryRemove(kvp.Key, out _);
    }
}