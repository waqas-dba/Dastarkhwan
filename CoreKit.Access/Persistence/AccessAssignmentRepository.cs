namespace CoreKit.Access.Persistence;

internal interface IAccessAssignmentRepository
{
    /// <summary>Read-only. The roles a user holds in a scope, with their permissions.</summary>
    Task<IReadOnlyList<AccessRole>> ListRolesForUserAsync(AccessScope scope, Guid userId, CancellationToken ct = default);

    /// <summary>Tracked. The user's assignments in a scope.</summary>
    Task<IReadOnlyList<AccessAssignment>> ListForUserForUpdateAsync(AccessScope scope, Guid userId, CancellationToken ct = default);

    /// <summary>How many people hold each role of the scope.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountByRoleAsync(AccessScope scope, CancellationToken ct = default);

    Task<int> CountForRoleAsync(Guid roleId, CancellationToken ct = default);

    void Add(AccessAssignment assignment);

    void Remove(AccessAssignment assignment);

    Task RemoveUserAsync(AccessScope scope, Guid userId, CancellationToken ct = default);
}

internal sealed class AccessAssignmentRepository : IAccessAssignmentRepository
{
    private readonly AccessDbContext _db;

    public AccessAssignmentRepository(AccessDbContext db) => _db = db;

    public async Task<IReadOnlyList<AccessRole>> ListRolesForUserAsync(
        AccessScope scope, Guid userId, CancellationToken ct = default)
        => await _db.Roles.AsNoTracking()
            .Include(r => r.Permissions)
            .Where(r => _db.Assignments.Any(a =>
                a.RoleId == r.Id && a.ScopeType == scope.Type && a.ScopeId == scope.Id && a.UserId == userId))
            .OrderBy(r => r.NormalizedName)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AccessAssignment>> ListForUserForUpdateAsync(
        AccessScope scope, Guid userId, CancellationToken ct = default)
        => await _db.Assignments
            .Where(a => a.ScopeType == scope.Type && a.ScopeId == scope.Id && a.UserId == userId)
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, int>> CountByRoleAsync(AccessScope scope, CancellationToken ct = default)
        => await _db.Assignments.AsNoTracking()
            .Where(a => a.ScopeType == scope.Type && a.ScopeId == scope.Id)
            .GroupBy(a => a.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, ct);

    public Task<int> CountForRoleAsync(Guid roleId, CancellationToken ct = default)
        => _db.Assignments.CountAsync(a => a.RoleId == roleId, ct);

    public void Add(AccessAssignment assignment) => _db.Assignments.Add(assignment);

    public void Remove(AccessAssignment assignment) => _db.Assignments.Remove(assignment);

    public async Task RemoveUserAsync(AccessScope scope, Guid userId, CancellationToken ct = default)
    {
        await _db.Assignments
            .Where(a => a.ScopeType == scope.Type && a.ScopeId == scope.Id && a.UserId == userId)
            .ExecuteDeleteAsync(ct);
    }
}