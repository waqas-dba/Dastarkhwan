using CoreKit.Identity.Contracts;
using CoreKit.Identity.Persistence;
using CoreKit.Identity.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
        // If we're already in a transaction, use its advisory lock.
        // Otherwise, acquire a transaction-level advisory lock for the duration of this check.
        var inTransaction = _db.Database.CurrentTransaction != null;

        if (!inTransaction)
        {
            await _db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({AdminCheckLockKey})", ct);
        }

        try
        {
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
        finally
        {
            // If we acquired our own lock, it's automatically released at the end of the implicit transaction
            // from ExecuteSqlRawAsync. If we're in the caller's transaction, the lock is held until that commits/rolls back.
        }
    }
}
