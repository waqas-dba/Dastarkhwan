namespace CoreKit.Tenant.Abstractions;

public interface ITenantEvent
{
    Guid TenantId { get; }

    DateTime OccurredAt { get; }
}

/// <summary>
/// Implement this in any module to react to tenant changes (for example, delete a tenant's data when it is
/// deleted). Handlers run in-process after the change is saved. A handler that throws is logged and does not
/// undo the change.
/// </summary>
public interface ITenantEventHandler<TEvent> where TEvent : ITenantEvent
{
    Task HandleAsync(TEvent tenantEvent, CancellationToken ct = default);
}

public sealed record TenantCreatedEvent(
    Guid TenantId, string Slug, string Name, TenantStatus Status, DateTime OccurredAt) : ITenantEvent;

public sealed record TenantUpdatedEvent(
    Guid TenantId, string Slug, string Name, DateTime OccurredAt) : ITenantEvent;

public sealed record TenantStatusChangedEvent(
    Guid TenantId, TenantStatus From, TenantStatus To, string? Reason, DateTime OccurredAt) : ITenantEvent;

public sealed record TenantDeletedEvent(
    Guid TenantId, string Slug, DateTime OccurredAt) : ITenantEvent;

public sealed record TenantMemberAddedEvent(
    Guid TenantId, Guid UserId, bool IsOwner, DateTime OccurredAt) : ITenantEvent;

public sealed record TenantMemberRemovedEvent(
    Guid TenantId, Guid UserId, DateTime OccurredAt) : ITenantEvent;

public sealed record TenantSettingChangedEvent(
    Guid TenantId, string Key, bool Removed, DateTime OccurredAt) : ITenantEvent;