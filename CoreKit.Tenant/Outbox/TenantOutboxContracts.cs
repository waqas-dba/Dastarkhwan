namespace CoreKit.Tenant.Outbox;

public sealed class TenantOutboxOptions
{
    public int BatchSize { get; set; } = 20;

    /// <summary>How long a claim is valid. A crashed publisher's messages become claimable again after this.</summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Maximum time one delivery may take. Must be shorter than <see cref="LeaseDuration"/>.</summary>
    public TimeSpan DeliveryTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Claims allowed before the message is dead-lettered.</summary>
    public int MaxAttempts { get; set; } = 10;

    public TimeSpan BaseBackoff { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);
}

/// <summary>What the transport hands the downstream consumer. It carries no Tenant entity types.</summary>
public sealed record TenantOutboxEnvelope(
    Guid EventId,
    Guid TenantId,
    long Sequence,
    string EventType,
    int SchemaVersion,
    Guid? SubjectUserId,
    long? MembershipGeneration,
    string PayloadJson,
    DateTime OccurredAt,
    int AttemptCount);

public enum TenantOutboxDeliveryOutcome
{
    /// <summary>The consumer durably committed the event (or durably recognised it as a duplicate).</summary>
    Accepted = 1,

    /// <summary>Temporary failure. Retry with backoff.</summary>
    RetryLater = 2,

    /// <summary>The consumer will never accept this event. Dead-letter it.</summary>
    Rejected = 3
}

public readonly record struct TenantOutboxDeliveryResult(TenantOutboxDeliveryOutcome Outcome, string? Error = null)
{
    public static TenantOutboxDeliveryResult Accepted() => new(TenantOutboxDeliveryOutcome.Accepted);

    public static TenantOutboxDeliveryResult RetryLater(string error) => new(TenantOutboxDeliveryOutcome.RetryLater, error);

    public static TenantOutboxDeliveryResult Rejected(string error) => new(TenantOutboxDeliveryOutcome.Rejected, error);
}

/// <summary>
/// The Tenant-owned port to the downstream consumer. Implementations MUST return Accepted only after the
/// consumer's own durable commit succeeded. An exception is treated as RetryLater. Delivery is at-least-once,
/// so the consumer must deduplicate on <see cref="TenantOutboxEnvelope.EventId"/>.
/// </summary>
public interface ITenantOutboxTransport
{
    Task<TenantOutboxDeliveryResult> DeliverAsync(TenantOutboxEnvelope envelope, CancellationToken ct);
}

/// <summary>A snapshot for dashboards and alerts.</summary>
public sealed record TenantOutboxStats(int Pending, int DeadLettered, TimeSpan? OldestPendingAge);