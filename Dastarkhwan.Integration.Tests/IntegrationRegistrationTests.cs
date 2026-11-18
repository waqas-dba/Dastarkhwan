using CoreKit.Access.Abstractions;
using CoreKit.Tenant.Abstractions;
using CoreKit.Tenant.Outbox;
using CoreKit.Tenant.Persistence;
using Dastarkhwan.Integration.Membership;
using Dastarkhwan.Integration.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dastarkhwan.Integration.Tests;

public sealed class IntegrationRegistrationTests
{
    private static ServiceCollection ServicesWithAccessPorts()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IAccessMembershipEventConsumer, FakeMembershipEventConsumer>();
        services.AddScoped<IAccessMembershipSnapshotInstaller, FakeSnapshotInstaller>();
        services.AddScoped<IAccessMembershipProjectionReader, FakeProjectionReader>();
        services.AddScoped<ITenantMembershipReader, FakeTenantMembershipReader>();
        services.AddScoped<ITenantMemberRepository, FakeTenantMemberRepository>();
        services.AddScoped<IAccessMaintenance, FakeAccessMaintenance>();
        return services;
    }

    [Fact]
    public void Registers_transport_and_gateway_and_resolves_them_with_scope_validation()
    {
        var services = ServicesWithAccessPorts();
        services.AddDastarkhwanIntegration();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();

        Assert.IsType<TenantToAccessOutboxTransport>(
            scope.ServiceProvider.GetRequiredService<ITenantOutboxTransport>());
        Assert.IsType<AccessTenantProjectionGateway>(
            scope.ServiceProvider.GetRequiredService<ITenantProjectionGateway>());
    }

    [Fact]
    public void Calling_twice_does_not_duplicate_registrations()
    {
        var services = ServicesWithAccessPorts();

        services.AddDastarkhwanIntegration();
        services.AddDastarkhwanIntegration();

        Assert.Single(services, d => d.ServiceType == typeof(ITenantOutboxTransport));
        Assert.Single(services, d => d.ServiceType == typeof(ITenantProjectionGateway));
    }

    [Fact]
    public void A_host_registration_wins_over_the_default()
    {
        var services = ServicesWithAccessPorts();
        services.AddScoped<ITenantOutboxTransport, HostTransport>();

        services.AddDastarkhwanIntegration();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.IsType<HostTransport>(scope.ServiceProvider.GetRequiredService<ITenantOutboxTransport>());
    }

    [Fact]
    public void Missing_access_registration_fails_on_resolution_instead_of_falling_back()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDastarkhwanIntegration();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Throws<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<ITenantOutboxTransport>());
    }

    private sealed class HostTransport : ITenantOutboxTransport
    {
        public Task<TenantOutboxDeliveryResult> DeliverAsync(TenantOutboxEnvelope envelope, CancellationToken ct)
            => Task.FromResult(TenantOutboxDeliveryResult.Accepted());
    }
}