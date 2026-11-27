using CoreKit.Access.Contracts;
using CoreKit.Tenant.Abstractions;

namespace Dastarkhwan.Integration.Membership;

/// <summary>
/// Reacts to the <see cref="TenantDeleted"/> integration event and removes everything Access holds for that
/// tenant scope: its roles, permissions and role assignments. Without it, a deleted tenant would leave its
/// authorization data behind.
/// </summary>
/// <remarks>
/// Runs in-process after the tenant deletion is saved. It is trusted system code and uses the unguarded
/// <c>IAccessMaintenance</c> interface. Removal is idempotent, so running it again is harmless.
/// </remarks>
public sealed class TenantDeletedToAccessCleanup : ITenantIntegrationEventHandler<TenantDeleted>
{
    private readonly IAccessMaintenance _maintenance;

    public TenantDeletedToAccessCleanup(IAccessMaintenance maintenance)
    {
        _maintenance = maintenance ?? throw new ArgumentNullException(nameof(maintenance));
    }

    public Task HandleAsync(TenantDeleted integrationEvent, CancellationToken ct = default)
        => _maintenance.RemoveScopeAsync(AccessScope.ForTenant(integrationEvent.TenantId), ct);
}