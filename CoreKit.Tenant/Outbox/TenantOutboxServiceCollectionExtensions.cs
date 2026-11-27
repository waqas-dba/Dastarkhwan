using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoreKit.Tenant.Outbox;

public static class TenantOutboxServiceCollectionExtensions
{
    public static IServiceCollection AddTenantOutboxPublisher(
        this IServiceCollection services, Action<TenantOutboxOptions>? configure = null)
    {
        var builder = services.AddOptions<TenantOutboxOptions>();
        if (configure is not null) builder.Configure(configure);
        builder
            .Validate(o => o.BatchSize >= 1, "Tenant outbox BatchSize must be at least 1.")
            .Validate(o => o.MaxAttempts >= 1, "Tenant outbox MaxAttempts must be at least 1.")
            .Validate(o => o.DeliveryTimeout > TimeSpan.Zero && o.DeliveryTimeout < o.LeaseDuration,
                "Tenant outbox DeliveryTimeout must be positive and shorter than LeaseDuration.")
            .Validate(o => o.BaseBackoff > TimeSpan.Zero && o.MaxBackoff >= o.BaseBackoff,
                "Tenant outbox backoff values are invalid.");

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<TenantOutboxDispatcher>();

        services.TryAddSingleton<ITenantOutboxTransport, NullTenantOutboxTransport>();
        services.AddHostedService<TenantOutboxPublisherService>();
        services.AddHostedService<TenantOutboxTransportGuard>();
        return services;
    }

    internal sealed class NullTenantOutboxTransport : ITenantOutboxTransport
    {
        public Task<TenantOutboxDeliveryResult> DeliverAsync(
            TenantOutboxEnvelope envelope, CancellationToken ct = default)
            => Task.FromResult(TenantOutboxDeliveryResult.RetryLater("No tenant outbox transport configured."));
    }

    private sealed class TenantOutboxTransportGuard : IHostedService
    {
        private readonly IServiceProvider _root;
        private readonly IHostEnvironment _env;
        private readonly ILogger<TenantOutboxTransportGuard> _log;

        public TenantOutboxTransportGuard(
            IServiceProvider root,
            IHostEnvironment env,
            ILogger<TenantOutboxTransportGuard> log)
        {
            _root = root;
            _env = env;
            _log = log;
        }

        public Task StartAsync(CancellationToken ct)
        {
            using var scope = _root.CreateScope();
            var transport = scope.ServiceProvider.GetRequiredService<ITenantOutboxTransport>();

            if (!_env.IsDevelopment() && transport is NullTenantOutboxTransport)
            {
                throw new InvalidOperationException(
                    "ITenantOutboxTransport is the null transport. Every tenant event would " +
                    "accumulate forever (RetryLater) and downstream consumers would never receive " +
                    "updates. Register a real ITenantOutboxTransport before deploying to non-Development.");
            }

            _log.LogInformation("Tenant outbox transport: {TransportType}", transport.GetType().Name);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
    }
}