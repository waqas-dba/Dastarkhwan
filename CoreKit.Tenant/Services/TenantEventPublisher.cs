namespace CoreKit.Tenant.Services;

public interface ITenantEventPublisher
{
    Task PublishAsync<TEvent>(TEvent tenantEvent, CancellationToken ct = default) where TEvent : ITenantEvent;
}

public sealed class TenantEventPublisher : ITenantEventPublisher
{
    private readonly IServiceProvider _services;
    private readonly ILogger<TenantEventPublisher> _logger;

    public TenantEventPublisher(IServiceProvider services, ILogger<TenantEventPublisher> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent tenantEvent, CancellationToken ct = default)
        where TEvent : ITenantEvent
    {
        foreach (var handler in _services.GetServices<ITenantEventHandler<TEvent>>())
        {
            try
            {
                await handler.HandleAsync(tenantEvent, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The change is already saved. One broken subscriber must not undo it or block the others.
                _logger.LogError(
                    ex, "A handler failed for {EventType} of tenant {TenantId}.", typeof(TEvent).Name, tenantEvent.TenantId);
            }
        }
    }
}