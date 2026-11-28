using CoreKit.Identity.Entities;
using CoreKit.Identity.Outbox;
using CoreKit.Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreKit.Identity.Outbox;

public sealed class IdentityOutboxDispatcher
{
    private const int MaxErrorLength = 512;

    private readonly IdentityDbContext _db;
    private readonly IIdentityOutboxTransport _transport;
    private readonly IdentityOutboxOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<IdentityOutboxDispatcher> _logger;
    private readonly string _leaseOwner =
        $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public IdentityOutboxDispatcher(
        IdentityDbContext db,
        IIdentityOutboxTransport transport,
        IOptions<IdentityOutboxOptions> options,
        TimeProvider time,
        ILogger<IdentityOutboxDispatcher> logger)
    {
        _db = db;
        _transport = transport;
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    public async Task<int> DispatchPendingAsync(CancellationToken ct = default)
    {
        var now = Now();
        var leaseUntil = now + _options.LeaseDuration;

        var candidateIds = await _db.OutboxMessages.AsNoTracking()
            .Where(m => m.Status == IdentityOutboxStatus.Pending
                && m.NextAttemptAt <= now
                && (m.LeaseExpiresAt == null || m.LeaseExpiresAt <= now))
            .OrderBy(m => m.CreatedAt)
            .Select(m => m.EventId)
            .Take(_options.BatchSize)
            .ToListAsync(ct);

        if (candidateIds.Count == 0) return 0;

        var claimed = new List<Guid>(candidateIds.Count);
        foreach (var id in candidateIds)
        {
            var rows = await _db.OutboxMessages
                .Where(m => m.EventId == id
                    && m.Status == IdentityOutboxStatus.Pending
                    && (m.LeaseExpiresAt == null || m.LeaseExpiresAt <= now))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.LeaseOwner, _leaseOwner)
                    .SetProperty(m => m.LeaseExpiresAt, (DateTime?)leaseUntil)
                    .SetProperty(m => m.AttemptCount, m => m.AttemptCount + 1)
                    .SetProperty(m => m.LastAttemptAt, (DateTime?)now), ct);

            if (rows == 1) claimed.Add(id);
        }

        if (claimed.Count == 0) return 0;

        var messages = await _db.OutboxMessages.AsNoTracking()
            .Where(m => claimed.Contains(m.EventId))
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        var delivered = 0;
        foreach (var message in messages)
        {
            ct.ThrowIfCancellationRequested();

            if (!await RenewLeaseAsync(message.EventId, ct))
                continue;

            if (await DeliverOneAsync(message, ct)) delivered++;
        }

        return delivered;
    }

    private async Task<bool> RenewLeaseAsync(Guid eventId, CancellationToken ct)
    {
        var leaseUntil = Now() + _options.LeaseDuration;
        var rows = await _db.OutboxMessages
            .Where(m => m.EventId == eventId
                && m.LeaseOwner == _leaseOwner
                && m.Status == IdentityOutboxStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.LeaseExpiresAt, (DateTime?)leaseUntil), ct);

        if (rows == 1) return true;

        _logger.LogWarning(
            "Identity outbox event {EventId} is no longer leased to this dispatcher; skipping delivery.",
            eventId);
        return false;
    }

    private async Task<bool> DeliverOneAsync(IdentityOutboxMessage message, CancellationToken ct)
    {
        var envelope = new IdentityOutboxEnvelope(
            message.EventId, message.EventType, message.SchemaVersion, message.SubjectUserId,
            message.PayloadJson, message.OccurredAt, message.AttemptCount);

        IdentityOutboxDeliveryResult result;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_options.DeliveryTimeout);
            result = await _transport.DeliverAsync(envelope, cts.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { result = IdentityOutboxDeliveryResult.RetryLater(ex.Message); }

        var now = Now();
        var error = Truncate(result.Error);

        if (result.Outcome == IdentityOutboxDeliveryOutcome.Accepted)
        {
            var rows = await _db.OutboxMessages
                .Where(m => m.EventId == message.EventId
                    && m.LeaseOwner == _leaseOwner
                    && m.Status == IdentityOutboxStatus.Pending)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.Status, IdentityOutboxStatus.Delivered)
                    .SetProperty(m => m.DeliveredAt, (DateTime?)now)
                    .SetProperty(m => m.LeaseOwner, (string?)null)
                    .SetProperty(m => m.LeaseExpiresAt, (DateTime?)null)
                    .SetProperty(m => m.LastError, (string?)null), ct);

            if (rows == 0)
                _logger.LogWarning(
                    "Identity outbox event {EventId} accepted but lease was lost; will be redelivered.",
                    message.EventId);
            else
                _logger.LogInformation(
                    "Identity outbox event {EventId} ({EventType}) delivered.",
                    message.EventId, message.EventType);

            return rows == 1;
        }

        var deadLetter = result.Outcome == IdentityOutboxDeliveryOutcome.Rejected
            || message.AttemptCount >= _options.MaxAttempts;

        if (deadLetter)
        {
            await _db.OutboxMessages
                .Where(m => m.EventId == message.EventId
                    && m.LeaseOwner == _leaseOwner
                    && m.Status == IdentityOutboxStatus.Pending)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.Status, IdentityOutboxStatus.DeadLettered)
                    .SetProperty(m => m.DeadLetteredAt, (DateTime?)now)
                    .SetProperty(m => m.LastError, error)
                    .SetProperty(m => m.LeaseOwner, (string?)null)
                    .SetProperty(m => m.LeaseExpiresAt, (DateTime?)null), ct);

            _logger.LogError(
                "Identity outbox event {EventId} ({EventType}) dead-lettered after {Attempts} attempts: {Error}",
                message.EventId, message.EventType, message.AttemptCount, error);
        }
        else
        {
            var next = now + ComputeBackoff(message.AttemptCount);
            await _db.OutboxMessages
                .Where(m => m.EventId == message.EventId
                    && m.LeaseOwner == _leaseOwner
                    && m.Status == IdentityOutboxStatus.Pending)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.NextAttemptAt, next)
                    .SetProperty(m => m.LastError, error)
                    .SetProperty(m => m.LeaseOwner, (string?)null)
                    .SetProperty(m => m.LeaseExpiresAt, (DateTime?)null), ct);

            _logger.LogWarning(
                "Identity outbox event {EventId} ({EventType}) will retry (attempt {Attempts}/{Max}) at {Next}: {Error}",
                message.EventId, message.EventType, message.AttemptCount, _options.MaxAttempts, next, error);
        }

        return false;
    }

    private TimeSpan ComputeBackoff(int attempt)
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