namespace CoreKit.Tenant.Events;

/// <summary>
/// The wire contract of the Tenant outbox. Consumers (Access) must not reference Tenant types, so the
/// contract is the event type string, the schema version and the JSON payload below.
/// </summary>
public static class TenantOutboxEventTypes
{
    public const int CurrentSchemaVersion = 1;

    // Membership events (Access projection input).
    public const string MemberAdded = "tenant.member.added";
    public const string MemberReactivated = "tenant.member.reactivated";
    public const string MemberRemoved = "tenant.member.removed";
    public const string MemberOwnershipChanged = "tenant.member.ownership_changed";

    // Lifecycle events.
    public const string TenantCreated = "tenant.lifecycle.created";
    public const string TenantStatusChanged = "tenant.lifecycle.status_changed";
    public const string TenantDeleted = "tenant.lifecycle.deleted";
}

/// <summary>
/// Status strings used on the wire. The values match <see cref="CoreKit.Tenant.Entities.TenantStatus"/> names.
/// "Deleted" exists only on the wire: the tenant row itself is gone.
/// </summary>
public static class TenantLifecycleStatuses
{
    public const string Pending = "Pending";
    public const string Active = "Active";
    public const string Suspended = "Suspended";
    public const string Archived = "Archived";
    public const string Deleted = "Deleted";
}

/// <summary>
/// Schema version 1 payload of every membership event. <c>MembershipGeneration</c> is the generation
/// AFTER the change. <c>IsActive</c> is false only for <c>tenant.member.removed</c>.
/// </summary>
public sealed record TenantMembershipEventPayload(
    Guid TenantId,
    Guid UserId,
    long MembershipGeneration,
    bool IsOwner,
    bool IsActive,
    DateTime OccurredAt);

/// <summary>
/// Schema version 1 payload of every lifecycle event. <c>FromStatus</c> is null for creation.
/// </summary>
public sealed record TenantLifecycleEventPayload(
    Guid TenantId,
    string? FromStatus,
    string Status,
    DateTime OccurredAt);