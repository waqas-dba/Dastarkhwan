namespace CoreKit.SharedKernel;

/// <summary>
/// A fact one module announces for others to react to. Integration events carry ids only, never entities,
/// so a module's internals stay private. They are delivered in-process, after the change is saved,
/// and a failing handler is logged but never undoes the change.
/// </summary>
public interface IIntegrationEvent
{
    DateTime OccurredAt { get; }
}

/// <summary>Implement this in any module to react to an integration event, and register it in DI.</summary>
public interface IEventHandler<TEvent> where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken ct = default);
}

/// <summary>A tenant was created.</summary>
public sealed record TenantCreated(Guid TenantId, DateTime OccurredAt) : IIntegrationEvent;

/// <summary>A tenant was permanently deleted. Other modules delete the data they hold for it.</summary>
public sealed record TenantDeleted(Guid TenantId, DateTime OccurredAt) : IIntegrationEvent;

/// <summary>A person left (or was removed from) a tenant.</summary>
public sealed record TenantMemberRemoved(Guid TenantId, Guid UserId, DateTime OccurredAt) : IIntegrationEvent;