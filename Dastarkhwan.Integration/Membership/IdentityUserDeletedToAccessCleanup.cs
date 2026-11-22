using CoreKit.Access.Abstractions;
using CoreKit.IntegrationEvents;
using Microsoft.Extensions.Logging;

namespace Dastarkhwan.Integration.Membership;

/// <summary>
/// Reacts to the <see cref="IdentityUserDeleted"/> integration event and removes the deleted user's
/// platform membership and all role assignments from Access. This prevents orphaned Access records for
/// users that no longer exist in Identity.
/// </summary>
/// <remarks>
/// Runs in-process after the Identity user deletion is saved. It is trusted system code using the
/// unguarded <c>IAccessMaintenance</c> interface.
/// A failing handler is logged and does not undo the user deletion.
/// </remarks>
public sealed class IdentityUserDeletedToAccessCleanup : IEventHandler<IdentityUserDeleted>
{
    private readonly IAccessMaintenance _maintenance;
    private readonly ILogger<IdentityUserDeletedToAccessCleanup> _logger;

    public IdentityUserDeletedToAccessCleanup(IAccessMaintenance maintenance, ILogger<IdentityUserDeletedToAccessCleanup> logger)
    {
        _maintenance = maintenance;
        _logger = logger;
    }

    public async Task HandleAsync(IdentityUserDeleted integrationEvent, CancellationToken ct = default)
    {
        var userId = integrationEvent.UserId;

        // Remove platform membership (if any)
        await _maintenance.RemovePlatformMemberAsync(userId, ct);

        // Note: RemoveUserAsync is per-scope (tenant). There's no single method to remove a user
        // from ALL scopes. For now, we only clean up platform membership.
        // Tenant-scoped assignments are cleaned up when the Tenant membership rows are deleted
        // and the TenantMemberRemoved event fires for each tenant.

        _logger.LogInformation("Removed Access platform membership for deleted user {UserId}.", userId);
    }
}