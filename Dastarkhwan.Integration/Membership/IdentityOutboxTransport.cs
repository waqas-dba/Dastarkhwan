using CoreKit.Abstractions.IntegrationEvents;
using CoreKit.Identity.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dastarkhwan.Integration.Membership;

/// <summary>
/// Delivers Identity outbox events to the registered in-process handlers.
/// This transport is used when the Identity outbox publisher runs and dispatches events.
/// </summary>
public sealed class IdentityOutboxTransport : IIdentityOutboxTransport
{
    private readonly IServiceProvider _services;
    private readonly ILogger<IdentityOutboxTransport> _logger;

    public IdentityOutboxTransport(IServiceProvider services, ILogger<IdentityOutboxTransport> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task<IdentityOutboxDeliveryResult> DeliverAsync(IdentityOutboxEnvelope envelope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        // Deserialize the event to get the actual type
        var eventType = Type.GetType($"CoreKit.Identity.IntegrationEvents.{envelope.EventType}, CoreKit.Identity");
        if (eventType == null)
        {
            _logger.LogWarning("Unknown Identity event type: {EventType}", envelope.EventType);
            return IdentityOutboxDeliveryResult.Rejected($"Unknown event type: {envelope.EventType}");
        }

        var integrationEvent = System.Text.Json.JsonSerializer.Deserialize(envelope.PayloadJson, eventType);
        if (integrationEvent == null)
        {
            return IdentityOutboxDeliveryResult.Rejected("Failed to deserialize event payload");
        }

        // Get all handlers for this event type
        var handlerType = typeof(IEventHandler<>).MakeGenericType(eventType);
        var handlers = _services.GetServices(handlerType);

        var handled = false;
        foreach (var handler in handlers)
        {
            try
            {
                var handleMethod = handlerType.GetMethod("HandleAsync");
                var task = (Task)handleMethod!.Invoke(handler, [integrationEvent, ct])!;
                await task;
                handled = true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Handler failed for Identity event {EventType}", envelope.EventType);
                return IdentityOutboxDeliveryResult.RetryLater(ex.Message);
            }
        }

        if (!handled)
        {
            _logger.LogWarning("No handlers registered for Identity event {EventType}", envelope.EventType);
        }

        return IdentityOutboxDeliveryResult.Accepted();
    }
}