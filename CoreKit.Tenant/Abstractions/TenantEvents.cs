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

/// <summary>
/// Base marker for cross-module integration events published by Tenant.
/// Other modules implement <see cref="ITenantIntegrationEventHandler{TEvent}"/> for these.
/// </summary>
public interface ITenantIntegrationEvent
{
    DateTime OccurredAt { get; }
}

/// <summary>
/// Implemented by another module (in practice, by the Integration layer) to react to a Tenant integration
/// event. Tenant owns this contract. Handlers run in-process after the change is saved; a handler that
/// throws is logged and does not undo the change.
/// </summary>
public interface ITenantIntegrationEventHandler<TEvent> where TEvent : ITenantIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken ct = default);
}

/// <summary>A tenant was created.</summary>
public sealed record TenantCreated(Guid TenantId, DateTime OccurredAt) : ITenantIntegrationEvent;

/// <summary>A tenant was permanently deleted. Other modules delete the data they hold for it.</summary>
public sealed record TenantDeleted(Guid TenantId, DateTime OccurredAt) : ITenantIntegrationEvent;

/// <summary>A person left (or was removed from) a tenant.</summary>
public sealed record TenantMemberRemoved(Guid TenantId, Guid UserId, DateTime OccurredAt) : ITenantIntegrationEvent;

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