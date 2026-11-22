namespace CoreKit.Identity.Outbox;

public sealed class IdentityOutboxOptions
{
    public int BatchSize { get; set; } = 20;

    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(2);

    public TimeSpan DeliveryTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public int MaxAttempts { get; set; } = 10;

    public TimeSpan BaseBackoff { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);
}

public sealed record IdentityOutboxEnvelope(
    Guid EventId,
    string EventType,
    int SchemaVersion,
    Guid? SubjectUserId,
    string PayloadJson,
    DateTime OccurredAt,
    int AttemptCount);

public enum IdentityOutboxDeliveryOutcome
{
    Accepted = 1,
    RetryLater = 2,
    Rejected = 3
}

public readonly record struct IdentityOutboxDeliveryResult(IdentityOutboxDeliveryOutcome Outcome, string? Error = null)
{
    public static IdentityOutboxDeliveryResult Accepted() => new(IdentityOutboxDeliveryOutcome.Accepted);

    public static IdentityOutboxDeliveryResult RetryLater(string error) => new(IdentityOutboxDeliveryOutcome.RetryLater, error);

    public static IdentityOutboxDeliveryResult Rejected(string error) => new(IdentityOutboxDeliveryOutcome.Rejected, error);
}

public interface IIdentityOutboxTransport
{
    Task<IdentityOutboxDeliveryResult> DeliverAsync(IdentityOutboxEnvelope envelope, CancellationToken ct);
}

public sealed record IdentityOutboxStats(int Pending, int DeadLettered, TimeSpan? OldestPendingAge);