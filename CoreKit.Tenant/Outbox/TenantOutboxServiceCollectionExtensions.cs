using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CoreKit.Tenant.Outbox;

public static class TenantOutboxServiceCollectionExtensions
{
    /// <summary>
    /// Opt-in. Adds the background publisher. The host (or PR-5's Access adapter) must also register an
    /// <see cref="ITenantOutboxTransport"/>; without one, a no-op transport is used that accepts every envelope.
    /// </summary>
    public static IServiceCollection AddTenantOutboxPublisher(
        this IServiceCollection services, Action<TenantOutboxOptions>? configure = null)
    {
        var builder = services.AddOptions<TenantOutboxOptions>();
        if (configure is not null)
            builder.Configure(configure);

        builder
            .Validate(o => o.BatchSize >= 1, "Tenant outbox BatchSize must be at least 1.")
            .Validate(o => o.MaxAttempts >= 1, "Tenant outbox MaxAttempts must be at least 1.")
            .Validate(o => o.DeliveryTimeout > TimeSpan.Zero && o.DeliveryTimeout < o.LeaseDuration,
                "Tenant outbox DeliveryTimeout must be positive and shorter than LeaseDuration.")
            .Validate(o => o.BaseBackoff > TimeSpan.Zero && o.MaxBackoff >= o.BaseBackoff,
                "Tenant outbox backoff values are invalid.");

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<TenantOutboxDispatcher>();
        
        // Default transport: returns Accepted (no-op). A host that wants durable delivery
        // (for example external side effects like emails, webhooks) must replace this with a real implementation.
        services.TryAddSingleton<ITenantOutboxTransport, NullTenantOutboxTransport>();
        
        services.AddHostedService<TenantOutboxPublisherService>();
        return services;
    }

    /// <summary>
    /// Default transport: accepts every envelope without side effects. A host that wants durable delivery
    /// (for example external side effects like emails, webhooks) must replace this with a real implementation.
    /// </summary>
    private sealed class NullTenantOutboxTransport : ITenantOutboxTransport
    {
        public Task<TenantOutboxDeliveryResult> DeliverAsync(
            TenantOutboxEnvelope envelope, CancellationToken ct = default)
            => Task.FromResult(TenantOutboxDeliveryResult.Accepted());
    }
}