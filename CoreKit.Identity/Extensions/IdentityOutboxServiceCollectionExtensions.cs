using CoreKit.Abstractions.IntegrationEvents;
using CoreKit.Identity.Interfaces;
using CoreKit.Identity.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CoreKit.Identity.Extensions;

public static class IdentityOutboxServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityOutbox(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<IdentityOutboxOptions>(configuration.GetSection("Identity:Outbox"));

        services.TryAddScoped<IIdentityEventPublisher, IdentityOutboxPublisher>();
        services.TryAddScoped<IdentityOutboxDispatcher>();
        services.TryAddSingleton<IIdentityOutboxTransport, NullIdentityOutboxTransport>();

        services.AddHostedService<IdentityOutboxTransportGuard>();
        services.AddHostedService<IdentityOutboxPublisherService>();

        return services;
    }

    private sealed class IdentityOutboxTransportGuard : IHostedService
    {
        private readonly IServiceProvider _services;
        public IdentityOutboxTransportGuard(IServiceProvider services) => _services = services;
        public Task StartAsync(CancellationToken ct)
        {
            using var scope = _services.CreateScope();
            var transport = scope.ServiceProvider.GetRequiredService<IIdentityOutboxTransport>();
            if (transport.GetType().Name.Contains("Null", StringComparison.Ordinal))
                throw new InvalidOperationException("Identity outbox transport is not configured (NullIdentityOutboxTransport).");
            return Task.CompletedTask;
        }
        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
    }
}