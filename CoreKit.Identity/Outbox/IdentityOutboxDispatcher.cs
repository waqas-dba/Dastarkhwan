using CoreKit.Abstractions.IntegrationEvents;
using CoreKit.Identity.Entities;
using CoreKit.Identity.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreKit.Identity.Outbox;

public sealed class IdentityOutboxDispatcher
{
    private readonly IdentityDbContext _db;
    private readonly IIdentityOutboxTransport _transport;
    private readonly IdentityOutboxOptions _options;
    private readonly ILogger<IdentityOutboxDispatcher> _logger;

    public IdentityOutboxDispatcher(
        IdentityDbContext db,
        IIdentityOutboxTransport transport,
        IOptions<IdentityOutboxOptions> options,
        ILogger<IdentityOutboxDispatcher> logger)
    {
        _db = db;
        _transport = transport;
        _options = options.Value;
        _logger = logger;
    }

    public async Task DispatchPendingAsync(CancellationToken ct = default)
    {
        var messages = await _db.OutboxMessages
            .Where(m => m.Status == IdentityOutboxStatus.Pending && m.NextAttemptAt <= DateTime.UtcNow)
            .OrderBy(m => m.CreatedAt)
            .Take(_options.BatchSize)
            .ToListAsync(ct);

        foreach (var message in messages)
        {
            await DispatchOneAsync(message, ct);
        }
    }

    private async Task DispatchOneAsync(IdentityOutboxMessage message, CancellationToken ct)
    {
        var leaseOwner = Environment.MachineName + "-" + Guid.NewGuid().ToString("N")[..8];
        var leaseExpires = DateTime.UtcNow.Add(_options.LeaseDuration);

        message.Status = IdentityOutboxStatus.Pending;
        message.AttemptCount++;
        message.LastAttemptAt = DateTime.UtcNow;
        message.NextAttemptAt = DateTime.UtcNow.Add(ComputeBackoff(message.AttemptCount));
        message.LeaseOwner = leaseOwner;
        message.LeaseExpiresAt = leaseExpires;
        message.LastError = null;

        await _db.SaveChangesAsync(ct);

        var envelope = new IdentityOutboxEnvelope(
            message.EventId,
            message.EventType,
            message.SchemaVersion,
            message.SubjectUserId,
            message.PayloadJson,
            message.OccurredAt,
            message.AttemptCount);

        IdentityOutboxDeliveryResult result;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_options.DeliveryTimeout);
            result = await _transport.DeliverAsync(envelope, cts.Token);
        }
        catch (OperationCanceledException)
        {
            result = IdentityOutboxDeliveryResult.RetryLater("Delivery timeout");
        }
        catch (Exception ex)
        {
            result = IdentityOutboxDeliveryResult.RetryLater(ex.Message);
        }

        if (result.Outcome == IdentityOutboxDeliveryOutcome.Accepted)
        {
            message.Status = IdentityOutboxStatus.Delivered;
            message.DeliveredAt = DateTime.UtcNow;
            message.LeaseOwner = null;
            message.LeaseExpiresAt = null;
            _logger.LogInformation("Identity outbox event {EventId} ({EventType}) delivered", message.EventId, message.EventType);
        }
        else if (result.Outcome == IdentityOutboxDeliveryOutcome.Rejected || message.AttemptCount >= _options.MaxAttempts)
        {
            message.Status = IdentityOutboxStatus.DeadLettered;
            message.DeadLetteredAt = DateTime.UtcNow;
            message.LastError = result.Error;
            message.LeaseOwner = null;
            message.LeaseExpiresAt = null;
            _logger.LogError("Identity outbox event {EventId} ({EventType}) dead-lettered after {Attempts} attempts: {Error}",
                message.EventId, message.EventType, message.AttemptCount, result.Error);
        }
        else
        {
            message.Status = IdentityOutboxStatus.Pending;
            message.LastError = result.Error;
            message.LeaseOwner = null;
            message.LeaseExpiresAt = null;
            _logger.LogWarning("Identity outbox event {EventId} ({EventType}) will retry (attempt {Attempts}/{MaxAttempts}): {Error}",
                message.EventId, message.EventType, message.AttemptCount, _options.MaxAttempts, result.Error);
        }

        await _db.SaveChangesAsync(ct);
    }

    private TimeSpan ComputeBackoff(int attempt)
    {
        var backoff = TimeSpan.FromSeconds(_options.BaseBackoff.TotalSeconds * Math.Pow(2, attempt - 1));
        return backoff > _options.MaxBackoff ? _options.MaxBackoff : backoff;
    }
}