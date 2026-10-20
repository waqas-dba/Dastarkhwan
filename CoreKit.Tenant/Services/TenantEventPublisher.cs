namespace CoreKit.Tenant.Services;

public interface ITenantEventPublisher
{
    Task PublishAsync<TEvent>(TEvent tenantEvent, CancellationToken ct = default) where TEvent : ITenantEvent;
}

/// <summary>
/// Delivers Tenant's events to their handlers. The three facts other modules care about are also announced
/// as small integration events from the shared kernel (ids only), so other modules can react
/// without referencing Tenant.
/// </summary>
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
            await RunAsync(() => handler.HandleAsync(tenantEvent, ct), typeof(TEvent).Name, tenantEvent.TenantId);

        switch (tenantEvent)
        {
            case TenantCreatedEvent created:
                await DispatchAsync(new TenantCreated(created.TenantId, created.OccurredAt), created.TenantId, ct);
                break;

            case TenantDeletedEvent deleted:
                await DispatchAsync(new TenantDeleted(deleted.TenantId, deleted.OccurredAt), deleted.TenantId, ct);
                break;

            case TenantMemberRemovedEvent removed:
                await DispatchAsync(
                    new TenantMemberRemoved(removed.TenantId, removed.UserId, removed.OccurredAt), removed.TenantId, ct);
                break;
        }
    }

    private async Task DispatchAsync<TIntegration>(TIntegration integrationEvent, Guid tenantId, CancellationToken ct)
        where TIntegration : IIntegrationEvent
    {
        foreach (var handler in _services.GetServices<IEventHandler<TIntegration>>())
            await RunAsync(() => handler.HandleAsync(integrationEvent, ct), typeof(TIntegration).Name, tenantId);
    }

    private async Task RunAsync(Func<Task> handle, string eventName, Guid tenantId)
    {
        try
        {
            await handle();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The change is already saved. One broken subscriber must not undo it or block the others.
            _logger.LogError(ex, "A handler failed for {EventType} of tenant {TenantId}.", eventName, tenantId);
        }
    }
}