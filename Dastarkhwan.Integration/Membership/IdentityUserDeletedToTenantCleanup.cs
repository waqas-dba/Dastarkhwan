using CoreKit.IntegrationEvents;
using CoreKit.Tenant.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dastarkhwan.Integration.Membership;

/// <summary>
/// Reacts to the <see cref="IdentityUserDeleted"/> integration event and removes the deleted user's
/// membership rows from all tenants. This prevents orphaned membership records for users that no longer
/// exist in Identity.
/// </summary>
/// <remarks>
/// Runs in-process after the Identity user deletion is saved. It is trusted system code.
/// A failing handler is logged and does not undo the user deletion.
/// </remarks>
public sealed class IdentityUserDeletedToTenantCleanup : IEventHandler<IdentityUserDeleted>
{
    private readonly TenantDbContext _db;
    private readonly ILogger<IdentityUserDeletedToTenantCleanup> _logger;

    public IdentityUserDeletedToTenantCleanup(TenantDbContext db, ILogger<IdentityUserDeletedToTenantCleanup> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task HandleAsync(IdentityUserDeleted integrationEvent, CancellationToken ct = default)
    {
        var userId = integrationEvent.UserId;

        // Find all membership rows for this user (including soft-deleted ones)
        var memberships = await _db.Members
            .IgnoreQueryFilters()
            .Where(m => m.UserId == userId)
            .ToListAsync(ct);

        if (memberships.Count == 0)
        {
            _logger.LogInformation("No Tenant membership rows found for deleted user {UserId}.", userId);
            return;
        }

        // Hard-delete the membership rows since the user no longer exists
        _db.Members.RemoveRange(memberships);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Removed {Count} Tenant membership row(s) for deleted user {UserId}.",
            memberships.Count, userId);
    }
}