using System.Diagnostics.Metrics;
using CoreKit.Tenant.Entities;
using CoreKit.Tenant.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreKit.Tenant.Outbox;

/// <summary>
/// At-least-once outbox publisher. Each database step is its own short statement in its own scope, and the
/// delivery to the consumer runs with NO DbContext or transaction open.
///
/// Ordering: only the lowest not-yet-delivered message of a tenant is eligible, so a tenant's events are
/// delivered one at a time in sequence order. A dead-lettered message blocks later events of that tenant.
/// Multiple instances: a claim is a conditional UPDATE, so only one instance wins a message.
/// Crashes: a crashed claim expires after the lease and is claimed again; the consumer deduplicates.
/// </summary>
public sealed class TenantOutboxDispatcher
{
    private static readonly Meter Meter = new("CoreKit.Tenant.Outbox");
    private static readonly Counter<long> DeliveredCounter = Meter.CreateCounter<long>("outbox.delivered");
    private static readonly Counter<long> RetriedCounter = Meter.CreateCounter<long>("outbox.retried");
    private static readonly Counter<long> DeadLetteredCounter = Meter.CreateCounter<long>("outbox.dead_lettered");
    private static readonly Counter<long> LeaseLostCounter = Meter.CreateCounter<long>("outbox.lease_lost");

    private const int MaxErrorLength = 2000;

    private readonly IServiceScopeFactory _scopes;
    private readonly TenantOutboxOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<TenantOutboxDispatcher> _logger;
    private readonly string _owner = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public TenantOutboxDispatcher(
        IServiceScopeFactory scopes,
        IOptions<TenantOutboxOptions> options,
        TimeProvider time,
        ILogger<TenantOutboxDispatcher> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    /// <summary>Claims one batch, delivers it, and returns how many messages were claimed.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        var batch = await ClaimBatchAsync(ct);

        foreach (var envelope in batch)
        {
            ct.ThrowIfCancellationRequested();

            if (!await RenewLeaseAsync(envelope, ct))
                continue;

            await DeliverOneAsync(envelope, ct);
        }
        return batch.Count;
    }

    public async Task<TenantOutboxStats> GetStatsAsync(CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantDbContext>();

        var pending = await db.OutboxMessages.CountAsync(m => m.Status == TenantOutboxStatus.Pending, ct);
        var dead = await db.OutboxMessages.CountAsync(m => m.Status == TenantOutboxStatus.DeadLettered, ct);
        var oldest = await db.OutboxMessages
            .Where(m => m.Status == TenantOutboxStatus.Pending)
            .OrderBy(m => m.CreatedAt)
            .Select(m => (DateTime?)m.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return new TenantOutboxStats(pending, dead, oldest is null ? null : Now() - oldest.Value);
    }

    private async Task<IReadOnlyList<TenantOutboxEnvelope>> ClaimBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantDbContext>();

        var now = Now();
        var leaseUntil = now + _options.LeaseDuration;

        var candidateIds = await db.OutboxMessages.AsNoTracking()
            .Where(m => m.Status == TenantOutboxStatus.Pending
                && m.NextAttemptAt <= now
                && (m.LeaseExpiresAt == null || m.LeaseExpiresAt <= now)
                && !db.OutboxMessages.Any(p => p.TenantId == m.TenantId
                    && p.Sequence < m.Sequence
                    && p.Status != TenantOutboxStatus.Delivered))
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Sequence)
            .Select(m => m.EventId)
            .Take(_options.BatchSize)
            .ToListAsync(ct);

        var claimed = new List<Guid>();
        foreach (var id in candidateIds)
        {
            // The conditional UPDATE is the claim: if another instance won it first, zero rows match.
            var rows = await db.OutboxMessages
                .Where(m => m.EventId == id
                            && m.Status == TenantOutboxStatus.Pending
                            && (m.LeaseExpiresAt == null || m.LeaseExpiresAt <= now))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.LeaseOwner, _owner)
                    .SetProperty(m => m.LeaseExpiresAt, (DateTime?)leaseUntil)
                    .SetProperty(m => m.AttemptCount, m => m.AttemptCount + 1)
                    .SetProperty(m => m.LastAttemptAt, (DateTime?)now), ct);

            if (rows == 1)
                claimed.Add(id);
        }

        if (claimed.Count == 0)
            return Array.Empty<TenantOutboxEnvelope>();

        var messages = await db.OutboxMessages.AsNoTracking()
            .Where(m => claimed.Contains(m.EventId))
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Sequence)
            .ToListAsync(ct);

        return messages.Select(m => new TenantOutboxEnvelope(
            m.EventId, m.TenantId, m.Sequence, m.EventType, m.SchemaVersion, m.SubjectUserId,
            m.MembershipGeneration, m.PayloadJson, m.OccurredAt, m.AttemptCount)).ToList();
    }

    private async Task<bool> RenewLeaseAsync(TenantOutboxEnvelope envelope, CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantDbContext>();

        var leaseUntil = Now() + _options.LeaseDuration;
        var rows = await db.OutboxMessages
            .Where(m => m.EventId == envelope.EventId
                && m.LeaseOwner == _owner
                && m.Status == TenantOutboxStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.LeaseExpiresAt, (DateTime?)leaseUntil), ct);

        if (rows == 1)
            return true;

        LeaseLostCounter.Add(1);
        _logger.LogWarning(
            "Outbox event {EventId} (tenant {TenantId}, sequence {Sequence}) is no longer leased to this dispatcher; skipping delivery.",
            envelope.EventId, envelope.TenantId, envelope.Sequence);
        return false;
    }

    private async Task DeliverOneAsync(TenantOutboxEnvelope envelope, CancellationToken ct)
    {
        TenantOutboxDeliveryResult result;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(_options.DeliveryTimeout);

            // No DbContext is open here. The transport gets its own scope and its own connections.
            await using var scope = _scopes.CreateAsyncScope();
            var transport = scope.ServiceProvider.GetRequiredService<ITenantOutboxTransport>();
            result = await transport.DeliverAsync(envelope, timeout.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutting down. Leave the lease alone: it expires and the message is claimed again.
            throw;
        }
        catch (Exception ex)
        {
            result = TenantOutboxDeliveryResult.RetryLater($"{ex.GetType().Name}: {ex.Message}");
        }

        switch (result.Outcome)
        {
            case TenantOutboxDeliveryOutcome.Accepted:
                await AcknowledgeAsync(envelope, ct);
                break;

            case TenantOutboxDeliveryOutcome.Rejected:
                await FailAsync(envelope, result.Error, deadLetter: true, ct);
                break;

            default:
                await FailAsync(envelope, result.Error, deadLetter: envelope.AttemptCount >= _options.MaxAttempts, ct);
                break;
        }
    }

    private async Task AcknowledgeAsync(TenantOutboxEnvelope envelope, CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
        var now = Now();

        // Only the current lease holder may acknowledge. If the lease was lost, the message is simply
        // delivered again later; that is safe because the consumer deduplicates on EventId.
        var rows = await db.OutboxMessages
            .Where(m => m.EventId == envelope.EventId
                        && m.LeaseOwner == _owner
                        && m.Status == TenantOutboxStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, TenantOutboxStatus.Delivered)
                .SetProperty(m => m.DeliveredAt, (DateTime?)now)
                .SetProperty(m => m.LeaseOwner, (string?)null)
                .SetProperty(m => m.LeaseExpiresAt, (DateTime?)null)
                .SetProperty(m => m.LastError, (string?)null), ct);

        if (rows == 1)
            DeliveredCounter.Add(1);
        else
        {
            LeaseLostCounter.Add(1);
            _logger.LogWarning(
                "Outbox event {EventId} was accepted but its lease was lost before acknowledgement; it will be redelivered.",
                envelope.EventId);
        }
    }

    private async Task FailAsync(TenantOutboxEnvelope envelope, string? error, bool deadLetter, CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
        var now = Now();
        var text = Truncate(error);

        var query = db.OutboxMessages.Where(m => m.EventId == envelope.EventId
                                                  && m.LeaseOwner == _owner
                                                  && m.Status == TenantOutboxStatus.Pending);

        int rows;
        if (deadLetter)
        {
            rows = await query.ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, TenantOutboxStatus.DeadLettered)
                .SetProperty(m => m.DeadLetteredAt, (DateTime?)now)
                .SetProperty(m => m.LeaseOwner, (string?)null)
                .SetProperty(m => m.LeaseExpiresAt, (DateTime?)null)
                .SetProperty(m => m.LastError, text), ct);

            if (rows == 1)
            {
                DeadLetteredCounter.Add(1);
                _logger.LogError(
                    "Outbox event {EventId} (tenant {TenantId}, sequence {Sequence}) was dead-lettered after {Attempts} attempts: {Error}. Later events of this tenant are blocked until it is resolved.",
                    envelope.EventId, envelope.TenantId, envelope.Sequence, envelope.AttemptCount, text);
            }
        }
        else
        {
            var next = now + Backoff(envelope.AttemptCount);
            rows = await query.ExecuteUpdateAsync(s => s
                .SetProperty(m => m.NextAttemptAt, next)
                .SetProperty(m => m.LeaseOwner, (string?)null)
                .SetProperty(m => m.LeaseExpiresAt, (DateTime?)null)
                .SetProperty(m => m.LastError, text), ct);

            if (rows == 1)
            {
                RetriedCounter.Add(1);
                _logger.LogWarning(
                    "Outbox event {EventId} delivery failed (attempt {Attempts}); retrying at {Next}: {Error}",
                    envelope.EventId, envelope.AttemptCount, next, text);
            }
        }

        if (rows != 1)
            LeaseLostCounter.Add(1);
    }

    private TimeSpan Backoff(int attempt)
    {
        var seconds = Math.Min(
            _options.MaxBackoff.TotalSeconds,
            _options.BaseBackoff.TotalSeconds * Math.Pow(2, Math.Max(0, attempt - 1)));
        var jitter = 1 + (Random.Shared.NextDouble() * 0.4 - 0.2);
        return TimeSpan.FromSeconds(Math.Min(_options.MaxBackoff.TotalSeconds, seconds * jitter));
    }

    private static string? Truncate(string? text)
        => text is { Length: > MaxErrorLength } ? text[..MaxErrorLength] : text;

    private DateTime Now() => _time.GetUtcNow().UtcDateTime;
}