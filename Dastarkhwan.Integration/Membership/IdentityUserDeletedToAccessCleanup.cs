using CoreKit.Abstractions.IntegrationEvents;
using CoreKit.Access.Abstractions;
using CoreKit.Identity.IntegrationEvents;
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

        // Remove user from all scopes (platform and all tenants)
        await _maintenance.RemoveUserFromAllScopesAsync(userId, ct);

        _logger.LogInformation("Removed Access membership and all role assignments for deleted user {UserId}.", userId);
    }
}