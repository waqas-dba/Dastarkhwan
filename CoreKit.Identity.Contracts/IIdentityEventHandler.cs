namespace CoreKit.Identity.Contracts;

/// <summary>
/// Implemented by another module (in practice, by the Integration layer) to react to an Identity integration
/// event. Identity owns this contract, so nothing outside Identity has to define an event-handler type for it.
/// </summary>
public interface IIdentityEventHandler<TEvent> where TEvent : IIdentityIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken ct = default);
}