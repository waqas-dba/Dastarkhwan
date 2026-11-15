namespace CoreKit.Tenant.Entities;

public enum TenantOutboxStatus
{
    /// <summary>Not yet acknowledged by the downstream consumer (includes leased and retrying messages).</summary>
    Pending = 1,

    /// <summary>The downstream consumer durably accepted the event.</summary>
    Delivered = 2,

    /// <summary>Delivery stopped (permanent rejection or attempts exhausted). Blocks later events of the same tenant.</summary>
    DeadLettered = 3
}

/// <summary>
/// One durable integration event. It is written in the same database transaction as the change it
/// describes, so a committed change always has its event and a rolled-back change leaves none.
/// There is deliberately no foreign key to the tenant: a TenantDeleted event must outlive the tenant row.
/// </summary>
public sealed class TenantOutboxMessage
{
    /// <summary>Stable, globally unique event identity. Consumers deduplicate on it.</summary>
    public Guid EventId { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>Per-tenant, gap-free, strictly increasing. Defines delivery and apply order.</summary>
    public long Sequence { get; set; }

    public string EventType { get; set; } = null!;

    public int SchemaVersion { get; set; }

    /// <summary>The member the event is about, when it is a membership event.</summary>
    public Guid? SubjectUserId { get; set; }

    /// <summary>The member's generation after the change, when it is a membership event.</summary>
    public long? MembershipGeneration { get; set; }

    public string PayloadJson { get; set; } = null!;

    public DateTime OccurredAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public TenantOutboxStatus Status { get; set; } = TenantOutboxStatus.Pending;

    /// <summary>Incremented when a publisher claims the message, so a crash loop still reaches the dead-letter limit.</summary>
    public int AttemptCount { get; set; }

    /// <summary>Earliest time the message may be claimed (retry backoff).</summary>
    public DateTime NextAttemptAt { get; set; }

    public string? LeaseOwner { get; set; }

    public DateTime? LeaseExpiresAt { get; set; }

    public DateTime? LastAttemptAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public DateTime? DeadLetteredAt { get; set; }

    public string? LastError { get; set; }
}