namespace CoreKit.Identity.Entities;

public enum IdentityOutboxStatus
{
    Pending = 1,
    Delivered = 2,
    DeadLettered = 3
}

/// <summary>
/// One durable integration event for Identity. Written in the same database transaction as the change it describes.
/// </summary>
public sealed class IdentityOutboxMessage
{
    public Guid EventId { get; set; }

    public string EventType { get; set; } = null!;

    public int SchemaVersion { get; set; }

    public Guid? SubjectUserId { get; set; }

    public string PayloadJson { get; set; } = null!;

    public DateTime OccurredAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public IdentityOutboxStatus Status { get; set; } = IdentityOutboxStatus.Pending;

    public int AttemptCount { get; set; }

    public DateTime NextAttemptAt { get; set; }

    public string? LeaseOwner { get; set; }

    public DateTime? LeaseExpiresAt { get; set; }

    public DateTime? LastAttemptAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public DateTime? DeadLetteredAt { get; set; }

    public string? LastError { get; set; }
}