using CoreKit.Access.Abstractions;
using CoreKit.Access.Extensions;
using CoreKit.Tenant.Events;

namespace CoreKit.Tenant.Integration;

/// <summary>
/// Reacts to the shared-kernel <see cref="TenantMemberRemoved"/> integration event and clears the
/// removed person's effective-authorization state in Access. Without this handler a person who leaves
/// a tenant keeps their cached tenant-scoped permissions until the Access cache entry expires, because
/// Access resolves membership through <c>IAccessMembership</c>, which reads the Tenant membership
/// cache, and nothing invalidates the Access cache when the membership row changes.
/// </summary>
/// <remarks>
/// Runs in-process after the Tenant change is saved. It is trusted system code: it uses the unguarded
/// <c>IAccessMaintenance</c> interface, which performs no permission check. A failing handler is logged
/// by the publisher and does not undo the membership change.
/// </remarks>
public sealed class TenantMemberRemovedToAccessMaintenance : IEventHandler<TenantMemberRemoved>
{
    private readonly IAccessMaintenance _maintenance;

    public TenantMemberRemovedToAccessMaintenance(IAccessMaintenance maintenance)
    {
        _maintenance = maintenance ?? throw new ArgumentNullException(nameof(maintenance));
    }

    public async Task HandleAsync(TenantMemberRemoved integrationEvent, CancellationToken ct = default)
    {
        // The scope is the tenant the person left. RemoveUserAsync deletes every role assignment the
        // person holds there and invalidates the Access cache entry, so the next authorization read
        // starts from a clean state.
        await _maintenance.RemoveUserAsync(
            AccessScope.ForTenant(integrationEvent.TenantId), integrationEvent.UserId, ct);
    }
}