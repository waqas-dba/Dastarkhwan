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

        services.AddHostedService<IdentityOutboxPublisherService>();

        return services;
    }

    private sealed class NullIdentityOutboxTransport : IIdentityOutboxTransport
    {
        public Task<IdentityOutboxDeliveryResult> DeliverAsync(IdentityOutboxEnvelope envelope, CancellationToken ct = default)
            => Task.FromResult(IdentityOutboxDeliveryResult.RetryLater("No transport configured"));
    }
}