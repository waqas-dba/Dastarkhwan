

namespace CoreKit.Identity.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly IdentityDbContext _db;

    public UserRepository(IdentityDbContext db) => _db = db;

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> FindByIdWithRolesAsync(Guid id, CancellationToken ct = default)
        => _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> FindForUpdateAsync(Guid id, CancellationToken ct = default)
        => _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> FindByNormalizedEmailWithRolesAsync(string normalizedEmail, CancellationToken ct = default)
        => _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);

    public Task<Guid?> FindUnconfirmedActiveIdByEmailAsync(string normalizedEmail, CancellationToken ct = default)
        => _db.Users
            .AsNoTracking()
            .Where(u => u.NormalizedEmail == normalizedEmail && u.IsActive && !u.EmailConfirmed)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(ct);

    public Task<bool> EmailExistsAsync(
        string normalizedEmail, Guid? excludingUserId = null, CancellationToken ct = default)
        => _db.Users.AnyAsync(
            u => u.NormalizedEmail == normalizedEmail &&
                 (excludingUserId == null || u.Id != excludingUserId.Value),
            ct);

    public Task<bool> MobileExistsAsync(
        string normalizedMobile, Guid? excludingUserId = null, CancellationToken ct = default)
        => _db.Users.AnyAsync(
            u => u.NormalizedMobile == normalizedMobile &&
                 (excludingUserId == null || u.Id != excludingUserId.Value),
            ct);

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> ListAsync(
        string? search, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        var users = _db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant();

            users = users.Where(u =>
                u.NormalizedEmail.Contains(term) ||
                (u.DisplayName != null && u.DisplayName.ToUpper().Contains(term)));
        }

        if (isActive.HasValue)
            users = users.Where(u => u.IsActive == isActive.Value);

        var totalCount = await users.CountAsync(ct);

        var items = await users
            .OrderBy(u => u.NormalizedEmail)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<Guid>> GetActiveUserIdsInRoleAsync(
        string normalizedRoleName, CancellationToken ct = default)
        => await _db.UserRoles
            .Where(ur => ur.Role.NormalizedName == normalizedRoleName && ur.Role.IsActive && ur.User.IsActive)
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(ct);

    public async Task<bool> CreateAsync(User user, CancellationToken ct = default)
    {
        _db.Users.Add(user);
        return await TrySaveAsync(user, ct);
    }

    public Task<bool> UpdateAsync(User user, CancellationToken ct = default)
        => TrySaveAsync(user, ct);

    public async Task<bool> RegisterFailedLoginAttemptAsync(
        Guid userId, DateTime now, DateTime lockoutEnd, int threshold, CancellationToken ct = default)
    {
        // One atomic UPDATE ... CASE transition. The database itself computes the new counter
        // value and decides whether a lockout is established, preserved, or cleared, against
        // the current row under a row lock, so concurrent requests cannot lose increments and
        // a stale clock or snapshot cannot clear a newer active lockout: every branch
        // condition is evaluated inside this single statement against the current row.
        var affected = await _db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.AccessFailedCount, u =>
                    u.LockoutEnd != null && u.LockoutEnd <= now
                        ? 1 // previous lockout has expired: fresh failure budget
                        : u.AccessFailedCount + 1)
                .SetProperty(u => u.LockoutEnd, u =>
                    u.LockoutEnd != null && u.LockoutEnd > now
                        ? u.LockoutEnd // active lockout: never extended or replaced by this attempt
                        : (u.LockoutEnd != null && u.LockoutEnd <= now ? 1 : u.AccessFailedCount + 1) >= threshold
                            ? (DateTime?)lockoutEnd
                            : (DateTime?)null),
                ct);

        return affected > 0;
    }

    public async Task<LoginCompletion> TryCompleteSuccessfulLoginAsync(
        Guid userId, int expectedSecurityVersion, DateTime now, CancellationToken ct = default)
    {
        // Authoritative success transition: the same single statement that resets the failure
        // state also refuses to touch an inactive, currently locked, or credential-version
        // stale account, evaluated against the current row. A concurrent failed attempt that
        // establishes a lockout, or a password change that bumps the security version, before
        // this runs makes the statement match zero rows, and the caller must not issue a
        // session. "Not locked" is !(LockoutEnd IS NOT NULL AND LockoutEnd > now), which is
        // null-safe for a cleared lockout.
        var affected = await _db.Users
            .Where(u => u.Id == userId
                && u.IsActive
                && u.SecurityVersion == expectedSecurityVersion
                && !(u.LockoutEnd != null && u.LockoutEnd > now))
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.AccessFailedCount, 0)
                .SetProperty(u => u.LockoutEnd, (DateTime?)null),
                ct);

        if (affected > 0)
            return LoginCompletion.Completed;

        // The transition was refused. Classify the reason from the current row; this read only
        // labels the refusal, the decision not to complete the login was already made above.
        var current = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive, u.SecurityVersion, u.LockoutEnd })
            .FirstOrDefaultAsync(ct);

        if (current is null)
            return LoginCompletion.UserMissing;
        if (!current.IsActive)
            return LoginCompletion.Inactive;
        if (current.LockoutEnd is { } lockoutEnd && lockoutEnd > now)
            return LoginCompletion.Locked;
        return LoginCompletion.VersionConflict;
    }

    public async Task<CredentialUpdateResult> TryChangePasswordAsync(
        Guid userId,
        string expectedCurrentHash,
        int expectedSecurityVersion,
        string newHash,
        DateTime now,
        bool bumpSecurityVersion,
        bool revokeSessions,
        CancellationToken ct = default)
    {
        // One transaction: the conditional password update, the optional security-version bump
        // and the optional refresh-token revocation all commit together or not at all. The
        // expected-hash and expected-version predicates run inside the UPDATE against the
        // current row, so two concurrent credential updates cannot silently overwrite each
        // other: only the request whose expectations still hold wins.
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var affected = await _db.Users
            .Where(u => u.Id == userId
                && u.PasswordHash == expectedCurrentHash
                && u.SecurityVersion == expectedSecurityVersion)
            .ExecuteUpdateAsync(s =>
            {
                s.SetProperty(u => u.PasswordHash, newHash)
                 .SetProperty(u => u.UpdatedAt, (DateTime?)now);

                if (bumpSecurityVersion)
                    s.SetProperty(u => u.SecurityVersion, u => u.SecurityVersion + 1);
            }, ct);

        if (affected == 0)
        {
            await transaction.RollbackAsync(ct);

            var current = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.PasswordHash, u.SecurityVersion })
                .FirstOrDefaultAsync(ct);

            if (current is null)
                return CredentialUpdateResult.UserMissing;
            return current.PasswordHash == expectedCurrentHash
                ? CredentialUpdateResult.VersionConflict
                : CredentialUpdateResult.HashConflict;
        }

        if (revokeSessions)
            await RefreshTokenRevocation.RevokeAllForUserAsync(_db, userId, now, ct);

        await transaction.CommitAsync(ct);

        return CredentialUpdateResult.Updated;
    }

    public async Task<CredentialUpdateResult> TryDeactivateAsync(
        Guid userId, int expectedSecurityVersion, DateTime now, CancellationToken ct = default)
    {
        // Same shape as TryChangePasswordAsync: the deactivation, the security-version bump and
        // the refresh-token revocation are one transaction. The expected-version predicate makes
        // the update conditional, so a concurrent security-sensitive update wins or loses
        // deterministically instead of being silently overwritten.
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var affected = await _db.Users
            .Where(u => u.Id == userId
                && u.IsActive
                && u.SecurityVersion == expectedSecurityVersion)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.IsActive, false)
                .SetProperty(u => u.SecurityVersion, u => u.SecurityVersion + 1)
                .SetProperty(u => u.UpdatedAt, (DateTime?)now),
                ct);

        if (affected == 0)
        {
            await transaction.RollbackAsync(ct);

            var exists = await _db.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == userId, ct);

            return exists ? CredentialUpdateResult.VersionConflict : CredentialUpdateResult.UserMissing;
        }

        await RefreshTokenRevocation.RevokeAllForUserAsync(_db, userId, now, ct);

        await transaction.CommitAsync(ct);

        return CredentialUpdateResult.Updated;
    }

    public async Task<bool> TryRehashPasswordAsync(
        Guid userId, string expectedCurrentHash, int expectedSecurityVersion, string newHash, DateTime now,
        CancellationToken ct = default)
    {
        // Conditional rehash: requires the expected existing hash and version, and never bumps
        // the version or revokes sessions. If the password changed concurrently the predicate
        // matches zero rows, so a stale rehash can never overwrite a newer password.
        var affected = await _db.Users
            .Where(u => u.Id == userId
                && u.PasswordHash == expectedCurrentHash
                && u.SecurityVersion == expectedSecurityVersion)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.PasswordHash, newHash)
                .SetProperty(u => u.UpdatedAt, (DateTime?)now),
                ct);

        return affected > 0;
    }

    public async Task ReplaceRolesAsync(User user, IReadOnlyCollection<Guid> roleIds, CancellationToken ct = default)
    {
        foreach (var link in user.UserRoles.Where(ur => !roleIds.Contains(ur.RoleId)).ToList())
            _db.UserRoles.Remove(link);

        var current = user.UserRoles.Select(ur => ur.RoleId).ToHashSet();

        foreach (var roleId in roleIds.Where(r => !current.Contains(r)))
            _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId });

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(User user, CancellationToken ct = default)
    {
        _db.Users.Remove(user); // role links and refresh tokens are removed by the database (cascade)
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Two requests can both pass an "email is free" or "mobile is free" check. The unique index
    /// then rejects the second one, and this turns that database error into a plain false.
    /// </summary>
    private async Task<bool> TrySaveAsync(User user, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            var normalizedEmail = user.NormalizedEmail;
            var normalizedMobile = user.NormalizedMobile;
            var userId = user.Id;

            _db.ChangeTracker.Clear();

            var emailConflict = await _db.Users.AnyAsync(
                u => u.NormalizedEmail == normalizedEmail && u.Id != userId, ct);

            var mobileConflict = normalizedMobile is not null &&
                await _db.Users.AnyAsync(
                    u => u.NormalizedMobile == normalizedMobile && u.Id != userId, ct);

            if (emailConflict || mobileConflict)
                return false;

            throw;
        }
    }
}