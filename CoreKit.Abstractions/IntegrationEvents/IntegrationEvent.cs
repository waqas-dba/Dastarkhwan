namespace CoreKit.Abstractions.IntegrationEvents;

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