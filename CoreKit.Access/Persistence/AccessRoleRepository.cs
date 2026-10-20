namespace CoreKit.Access.Persistence;

internal interface IAccessRoleRepository
{
    /// <summary>Read-only, with permissions, ordered by name.</summary>
    Task<IReadOnlyList<AccessRole>> ListAsync(AccessScope scope, CancellationToken ct = default);

    /// <summary>Read-only, with permissions.</summary>
    Task<AccessRole?> FindAsync(AccessScope scope, Guid id, CancellationToken ct = default);

    /// <summary>Tracked, with permissions.</summary>
    Task<AccessRole?> FindForUpdateAsync(AccessScope scope, Guid id, CancellationToken ct = default);

    /// <summary>Read-only, with permissions. Ids that do not exist in the scope are missing from the result.</summary>
    Task<IReadOnlyList<AccessRole>> FindManyAsync(AccessScope scope, IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    Task<int> CountAsync(AccessScope scope, CancellationToken ct = default);

    Task<bool> NameExistsAsync(AccessScope scope, string normalizedName, Guid? excludingRoleId, CancellationToken ct = default);

    void Add(AccessRole role);

    void Remove(AccessRole role);

    /// <summary>Deletes every role of the scope at once. Permissions and assignments follow (cascade).</summary>
    Task RemoveScopeAsync(AccessScope scope, CancellationToken ct = default);
}

internal sealed class AccessRoleRepository : IAccessRoleRepository
{
    private readonly AccessDbContext _db;

    public AccessRoleRepository(AccessDbContext db) => _db = db;

    public async Task<IReadOnlyList<AccessRole>> ListAsync(AccessScope scope, CancellationToken ct = default)
        => await _db.Roles.AsNoTracking()
            .Include(r => r.Permissions)
            .Where(r => r.ScopeType == scope.Type && r.ScopeId == scope.Id)
            .OrderBy(r => r.NormalizedName)
            .ToListAsync(ct);

    public Task<AccessRole?> FindAsync(AccessScope scope, Guid id, CancellationToken ct = default)
        => _db.Roles.AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == id && r.ScopeType == scope.Type && r.ScopeId == scope.Id, ct);

    public Task<AccessRole?> FindForUpdateAsync(AccessScope scope, Guid id, CancellationToken ct = default)
        => _db.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == id && r.ScopeType == scope.Type && r.ScopeId == scope.Id, ct);

    public async Task<IReadOnlyList<AccessRole>> FindManyAsync(
        AccessScope scope, IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return new List<AccessRole>();

        return await _db.Roles.AsNoTracking()
            .Include(r => r.Permissions)
            .Where(r => r.ScopeType == scope.Type && r.ScopeId == scope.Id && ids.Contains(r.Id))
            .ToListAsync(ct);
    }

    public Task<int> CountAsync(AccessScope scope, CancellationToken ct = default)
        => _db.Roles.CountAsync(r => r.ScopeType == scope.Type && r.ScopeId == scope.Id, ct);

    public Task<bool> NameExistsAsync(
        AccessScope scope, string normalizedName, Guid? excludingRoleId, CancellationToken ct = default)
        => _db.Roles.AnyAsync(
            r => r.ScopeType == scope.Type && r.ScopeId == scope.Id && r.NormalizedName == normalizedName &&
                 (excludingRoleId == null || r.Id != excludingRoleId.Value), ct);

    public void Add(AccessRole role) => _db.Roles.Add(role);

    public void Remove(AccessRole role) => _db.Roles.Remove(role);

    public async Task RemoveScopeAsync(AccessScope scope, CancellationToken ct = default)
    {
        await _db.Roles
            .Where(r => r.ScopeType == scope.Type && r.ScopeId == scope.Id)
            .ExecuteDeleteAsync(ct);
    }
}