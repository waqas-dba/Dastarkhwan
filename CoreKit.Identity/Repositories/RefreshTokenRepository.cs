using CoreKit.Identity.Entities;
using CoreKit.Identity.Interfaces;
using CoreKit.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreKit.Identity.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IdentityDbContext _db;

    public RefreshTokenRepository(IdentityDbContext db) => _db = db;

    public Task<RefreshToken?> FindByHashWithUserAsync(string tokenHash, CancellationToken ct = default)
        => _db.RefreshTokens
            .AsNoTracking()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task<GuardedIssuanceResult> TryIssueGuardedAsync(
        Guid userId,
        int expectedSecurityVersion,
        bool requireUnlocked,
        DateTime now,
        RefreshToken token,
        CancellationToken ct = default)
    {
        // One transaction on the shared scoped IdentityDbContext covers the user-row guard and
        // the token insert. The guard is an UPDATE that assigns the security version to itself:
        // it changes no data but takes a write row lock on the user, held until commit, so the
        // re-check and the insert are atomic with respect to concurrent credential changes,
        // deactivation and (for login) lockout. Works identically on PostgreSQL (row lock) and
        // SQLite (statement-level write serialization).
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            var guarded = await _db.Users
                .Where(u => u.Id == userId
                    && u.IsActive
                    && u.SecurityVersion == expectedSecurityVersion
                    && (!requireUnlocked || u.LockoutEnd == null || u.LockoutEnd <= now))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.SecurityVersion, u => u.SecurityVersion),
                    ct);

            if (guarded == 0)
            {
                await transaction.RollbackAsync(ct);
                return GuardedIssuanceResult.UserUnavailable;
            }

            _db.RefreshTokens.Add(token);
            await _db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
            return GuardedIssuanceResult.Issued;
        }
        catch (DbUpdateException)
        {
            // The insert violated a database constraint (for example the unique token hash).
            // Roll back and leave the context clean: the rejected token must not linger in the
            // change tracker and be retried by a later SaveChanges.
            await transaction.RollbackAsync(CancellationToken.None);
            _db.Entry(token).State = EntityState.Detached;
            return GuardedIssuanceResult.InsertFailed;
        }
    }

    public async Task<bool> TryRevokeAsync(Guid tokenId, string reason, DateTime now, CancellationToken ct = default)
    {
        var changed = await _db.RefreshTokens
            .Where(t => t.Id == tokenId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, (DateTime?)now)
                .SetProperty(t => t.RevokedReason, reason), ct);

        return changed > 0;
    }

    public async Task RevokeByHashAsync(string tokenHash, string reason, DateTime now, CancellationToken ct = default)
    {
        await _db.RefreshTokens
            .Where(t => t.TokenHash == tokenHash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, (DateTime?)now)
                .SetProperty(t => t.RevokedReason, reason), ct);
    }

    public Task<int> RevokeAllForUserAsync(Guid userId, string reason, DateTime now, CancellationToken ct = default)
        => _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, (DateTime?)now)
                .SetProperty(t => t.RevokedReason, reason), ct);

    public async Task<bool> HasActiveTokenInFamilyAsync(Guid familyId, DateTime now, CancellationToken ct = default)
        => await _db.RefreshTokens
            .AnyAsync(t => t.FamilyId == familyId && t.RevokedAt == null && t.ExpiresAt > now, ct);

    public Task<int> RevokeAllInFamilyAsync(Guid familyId, string reason, DateTime now, CancellationToken ct = default)
        => _db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, (DateTime?)now)
                .SetProperty(t => t.RevokedReason, reason), ct);

    public async Task<RotationResult> TryRotateAsync(
        Guid userId,
        Guid oldTokenId,
        Guid familyId,
        int expectedSecurityVersion,
        DateTime now,
        string reason,
        RefreshToken newToken,
        CancellationToken ct = default)
    {
        // Atomic rotation: revoke old token + issue new token in one transaction with user-row guard.
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            // Guard: re-check user state under row lock
            var guarded = await _db.Users
                .Where(u => u.Id == userId
                    && u.IsActive
                    && u.SecurityVersion == expectedSecurityVersion)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.SecurityVersion, u => u.SecurityVersion),
                    ct);

            if (guarded == 0)
            {
                await transaction.RollbackAsync(ct);
                return RotationResult.UserUnavailable;
            }

            // Revoke old token. If it was already revoked, this is replay.
            var oldTokenRevoked = await _db.RefreshTokens
                .Where(t => t.Id == oldTokenId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.RevokedAt, (DateTime?)now)
                    .SetProperty(t => t.RevokedReason, reason), ct);

            if (oldTokenRevoked == 0)
            {
                // Old token already revoked = replay detected. Revoke the whole family.
                await _db.RefreshTokens
                    .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(t => t.RevokedAt, (DateTime?)now)
                        .SetProperty(t => t.RevokedReason, RefreshTokenRevokedReasons.ReplayDetected), ct);
                await transaction.CommitAsync(ct);
                return RotationResult.ReplayDetected;
            }

            // Issue new token with same family ID
            _db.RefreshTokens.Add(newToken);
            await _db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
            return RotationResult.Rotated;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _db.Entry(newToken).State = EntityState.Detached;
            return RotationResult.InsertFailed;
        }
    }
}