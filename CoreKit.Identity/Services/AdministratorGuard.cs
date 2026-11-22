using CoreKit.Identity.Interfaces;
using CoreKit.Identity.Persistence;
using CoreKit.Identity.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CoreKit.Identity.Services;

/// <summary>
/// Prevents the one mistake that locks everybody out: removing, deactivating or deleting the last
/// administrator. Delegates to an <see cref="IPlatformAdministratorProvider"/> (implemented by Access)
/// for the authoritative admin list, and verifies the user is active in Identity.
/// Uses PostgreSQL advisory locks for distributed concurrency control.
/// </summary>
public sealed class AdministratorGuard : IAdministratorGuard
{
    private readonly IPlatformAdministratorProvider _provider;
    private readonly IUserRepository _userRepository;
    private readonly IdentityDbContext _db;

    // Advisory lock key for "last admin check" - fixed 64-bit integer
    private const long AdminCheckLockKey = 0x4C41535441444D4E; // "LASTADMN" in hex

    public AdministratorGuard(IPlatformAdministratorProvider provider, IUserRepository userRepository, IdentityDbContext db)
    {
        _provider = provider;
        _userRepository = userRepository;
        _db = db;
    }

    public async Task<bool> IsLastActiveAdministratorAsync(Guid userId, CancellationToken ct = default)
    {
        // Acquire PostgreSQL transaction-level advisory lock to serialize the check
        // This works across multiple app instances and is automatically released at transaction end
        await _db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({AdminCheckLockKey})", ct);

        var adminUserIds = await _provider.GetPlatformAdministratorUserIdsAsync(ct);

        // Filter out inactive Identity accounts. Access role holders may include users
        // that have been deactivated in Identity; they should not count as active admins.
        var activeAdminIds = new List<Guid>();
        foreach (var adminId in adminUserIds)
        {
            var user = await _userRepository.FindByIdAsync(adminId, ct);
            if (user is not null && user.IsActive)
            {
                activeAdminIds.Add(adminId);
            }
        }

        if (activeAdminIds.Count != 1)
            return false;

        var soleAdminId = activeAdminIds[0];
        return soleAdminId == userId;
    }
}
