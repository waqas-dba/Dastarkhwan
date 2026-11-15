using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CoreKit.Tenant.Outbox;

public static class TenantOutboxServiceCollectionExtensions
{
    /// <summary>
    /// Opt-in. Adds the background publisher. The host (or PR-5's Access adapter) must also register an
    /// <see cref="ITenantOutboxTransport"/>; without one, delivery fails loudly and retries.
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
        services.AddHostedService<TenantOutboxPublisherService>();
        return services;
    }
}