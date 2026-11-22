namespace CoreKit.Identity.Interfaces;

/// <summary>
/// Base marker for integration events published by Identity.
/// </summary>
public interface IIdentityIntegrationEvent : CoreKit.Abstractions.IntegrationEvents.IIntegrationEvent
{
}

/// <summary>
/// Publishes Identity integration events for other modules to react to.
/// Implemented by the host or integration layer.
/// </summary>
public interface IIdentityEventPublisher
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct = default)
        where TEvent : IIdentityIntegrationEvent;
}