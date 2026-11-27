using CoreKit.Identity.Contracts;
using CoreKit.Tenant.Abstractions;
using Microsoft.Extensions.Logging;

namespace Dastarkhwan.Integration.Membership;

/// <summary>
/// Reacts to the <see cref="IdentityUserDeleted"/> integration event and removes the deleted user's
/// membership rows from all tenants by calling the Tenant-owned membership service.
/// This prevents orphaned membership records for users that no longer
/// exist in Identity.
/// </summary>
/// <remarks>
/// Runs in-process after the Identity user deletion is saved. It is trusted system code.
/// A failing handler is logged and does not undo the user deletion.
/// </remarks>
public sealed class IdentityUserDeletedToTenantCleanup : IIdentityEventHandler<IdentityUserDeleted>
{
    private readonly ITenantMembershipService _membershipService;
    private readonly ILogger<IdentityUserDeletedToTenantCleanup> _logger;

    public IdentityUserDeletedToTenantCleanup(ITenantMembershipService membershipService, ILogger<IdentityUserDeletedToTenantCleanup> logger)
    {
        _membershipService = membershipService;
        _logger = logger;
    }

    public async Task HandleAsync(IdentityUserDeleted integrationEvent, CancellationToken ct = default)
    {
        var userId = integrationEvent.UserId;

        // Call Tenant-owned service to remove all memberships for the deleted user
        var count = await _membershipService.RemoveAllMembershipsForDeletedUserAsync(userId, ct);

        if (count == 0)
        {
            _logger.LogInformation("No Tenant membership rows found for deleted user {UserId}.", userId);
            return;
        }

        _logger.LogInformation(
            "Removed {Count} Tenant membership row(s) for deleted user {UserId}.",
            count, userId);
    }
}