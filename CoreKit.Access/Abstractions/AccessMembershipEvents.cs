namespace CoreKit.Access.Abstractions;

/// <summary>
/// The Tenant event contract as Access understands it. These strings and the JSON payload shapes are the
/// wire contract with Tenant; Access does not reference Tenant, so the contract is data, not shared types.
/// </summary>
public static class AccessMembershipEventTypes
{
    public const int SupportedSchemaVersion = 1;

    public const string MemberAdded = "tenant.member.added";
    public const string MemberReactivated = "tenant.member.reactivated";
    public const string MemberRemoved = "tenant.member.removed";
    public const string MemberOwnershipChanged = "tenant.member.ownership_changed";

    public const string TenantCreated = "tenant.lifecycle.created";
    public const string TenantStatusChanged = "tenant.lifecycle.status_changed";
    public const string TenantDeleted = "tenant.lifecycle.deleted";
}

/// <summary>Tenant lifecycle states Access understands. Only <see cref="Active"/> authorizes.</summary>
public static class AccessTenantStatuses
{
    public const string Pending = "Pending";
    public const string Active = "Active";
    public const string Suspended = "Suspended";
    public const string Archived = "Archived";
    public const string Deleted = "Deleted";

    public static bool IsKnown(string? status)
        => status is Pending or Active or Suspended or Archived or Deleted;
}

/// <summary>One event as the transport delivers it. The payload is the Tenant JSON contract.</summary>
public sealed record AccessMembershipEventEnvelope(
    Guid EventId,
    Guid TenantId,
    long Sequence,
    string EventType,
    int SchemaVersion,
    Guid? SubjectUserId,
    long? MembershipGeneration,
    string PayloadJson,
    DateTime OccurredAt);

public enum AccessMembershipEventOutcome
{
    /// <summary>Applied and durably committed.</summary>
    Applied = 1,

    /// <summary>Already applied earlier. Nothing changed. Safe to acknowledge.</summary>
    Duplicate = 2,

    /// <summary>Not applicable yet (sequence gap) or a temporary failure. Deliver again later.</summary>
    RetryLater = 3,

    /// <summary>Permanently unacceptable. Do not retry.</summary>
    Rejected = 4
}

public readonly record struct AccessMembershipEventResult(AccessMembershipEventOutcome Outcome, string? Reason = null)
{
    public static AccessMembershipEventResult Applied() => new(AccessMembershipEventOutcome.Applied);

    public static AccessMembershipEventResult Duplicate(string? reason = null) => new(AccessMembershipEventOutcome.Duplicate, reason);

    public static AccessMembershipEventResult RetryLater(string reason) => new(AccessMembershipEventOutcome.RetryLater, reason);

    public static AccessMembershipEventResult Rejected(string reason) => new(AccessMembershipEventOutcome.Rejected, reason);
}

/// <summary>
/// Access's durable consumer of Tenant events (membership and lifecycle). Applied and Duplicate both mean
/// "Access durably holds this event", so a publisher may acknowledge on either. Delivery is at-least-once;
/// the consumer deduplicates.
/// </summary>
public interface IAccessMembershipEventConsumer
{
    Task<AccessMembershipEventResult> ApplyAsync(AccessMembershipEventEnvelope envelope, CancellationToken ct = default);
}