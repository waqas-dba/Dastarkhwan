using CoreKit.IntegrationEvents;

namespace CoreKit.Identity.Interfaces;

/// <summary>
/// Publishes Identity integration events for other modules to react to.
/// Implemented by the host or integration layer.
/// </summary>
public interface IIdentityEventPublisher
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct = default)
        where TEvent : IIntegrationEvent;
}