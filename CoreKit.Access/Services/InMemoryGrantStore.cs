using System.Collections.Concurrent;
using CoreKit.Access.Contracts;

namespace CoreKit.Access.Services;

internal sealed class InMemoryGrantStore : IGrantStore
{
    private static readonly TimeSpan RetentionGrace = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MaxRetention = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<Guid, DateTime> _consumed = new();
    private readonly TimeProvider _time;
    private readonly TimeSpan _pruneInterval = TimeSpan.FromMinutes(1);
    private DateTime _nextPruneUtc = DateTime.MinValue;
    private readonly object _pruneGate = new();

    public InMemoryGrantStore(TimeProvider time) => _time = time;

    public Task<bool> TryConsumeAsync(Guid grantId, DateTime expiresAtUtc, CancellationToken ct = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        MaybePrune(now);

        var ceiling = now + MaxRetention;
        var retainUntil = expiresAtUtc + RetentionGrace;
        if (retainUntil > ceiling)
            retainUntil = ceiling;

        return Task.FromResult(_consumed.TryAdd(grantId, retainUntil));
    }

    private void MaybePrune(DateTime now)
    {
        lock (_pruneGate)
        {
            if (now < _nextPruneUtc) return;
            _nextPruneUtc = now + _pruneInterval;
        }

        foreach (var kvp in _consumed)
            if (kvp.Value < now)
                _consumed.TryRemove(kvp.Key, out _);
    }
}