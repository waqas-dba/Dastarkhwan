

namespace CoreKit.IAM.Repositories;

internal sealed class UserRepository : IUserRepository
{
    private readonly IamDbContext _db;

    public UserRepository(IamDbContext db) => _db = db;

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

    public async Task SetPasswordHashAsync(
        Guid userId, string passwordHash, DateTime now, CancellationToken ct = default)
    {
        await _db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.PasswordHash, passwordHash)
                .SetProperty(u => u.UpdatedAt, (DateTime?)now), ct);
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
    /// Two requests can both pass an "email is free" check. The unique index then rejects the second
    /// one, and this turns that database error into a plain false.
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
            var userId = user.Id;

            _db.ChangeTracker.Clear();

            if (await _db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail && u.Id != userId, ct))
                return false;

            throw;
        }
    }
}