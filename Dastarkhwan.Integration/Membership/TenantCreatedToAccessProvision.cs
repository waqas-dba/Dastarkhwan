using CoreKit.Abstractions.IntegrationEvents;
using CoreKit.Access.Contracts;
using CoreKit.Tenant.Abstractions;

namespace Dastarkhwan.Integration.Membership;

/// <summary>
/// Reacts to the <see cref="TenantCreated"/> integration event and provisions the default
/// roles for the new tenant in Access. This ensures new tenants immediately have Manager and Cashier
/// role templates available for assignment.
/// </summary>
/// <remarks>
/// Runs in-process after the Tenant change is saved. It is trusted system code: it uses the unguarded
/// <c>IAccessMaintenance</c> interface, which performs no permission check. A failing handler is logged
/// by the publisher and does not undo the tenant creation.
/// </remarks>
public sealed class TenantCreatedToAccessProvision : IEventHandler<TenantCreated>
{
    private readonly IAccessMaintenance _maintenance;

    public TenantCreatedToAccessProvision(IAccessMaintenance maintenance)
    {
        _maintenance = maintenance ?? throw new ArgumentNullException(nameof(maintenance));
    }

    public async Task HandleAsync(TenantCreated integrationEvent, CancellationToken ct = default)
    {
        var scope = AccessScope.ForTenant(integrationEvent.TenantId);
        await _maintenance.ProvisionScopeAsync(scope, ct);
    }
}