namespace CoreKit.Access.Persistence;

internal interface IAccessAssignmentRepository
{
    /// <summary>Read-only. The roles a user holds in a scope, with their permissions.</summary>
    Task<IReadOnlyList<AccessRole>> ListRolesForUserAsync(AccessScope scope, Guid userId, long membershipGeneration, CancellationToken ct = default);

    /// <summary>Tracked. The user's assignments in a scope.</summary>
    Task<IReadOnlyList<AccessAssignment>> ListForUserForUpdateAsync(AccessScope scope, Guid userId, CancellationToken ct = default);

    /// <summary>How many people hold each role of the scope.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountByRoleAsync(AccessScope scope, CancellationToken ct = default);

    /// <summary>Read-only. User ids holding a role by normalized name in a scope. When a generation is given,
    /// only assignments at that generation are counted (otherwise every generation).</summary>
    Task<IReadOnlyList<Guid>> ListUserIdsWithRoleAsync(
        AccessScope scope, string normalizedRoleName, long? membershipGeneration, CancellationToken ct = default);

    Task<int> CountForRoleAsync(Guid roleId, CancellationToken ct = default);

    void Add(AccessAssignment assignment);

    void Remove(AccessAssignment assignment);

    /// <summary>Stages deletion of every assignment the user holds in the scope, for tracked removal.
    /// The unit of work persists the staged deletions together with the rest of the change.</summary>
    Task RemoveUserAsync(AccessScope scope, Guid userId, CancellationToken ct = default);
}

internal sealed class AccessAssignmentRepository : IAccessAssignmentRepository
{
    private readonly AccessDbContext _db;

    public AccessAssignmentRepository(AccessDbContext db) => _db = db;

    public async Task<IReadOnlyList<AccessRole>> ListRolesForUserAsync(
        AccessScope scope, Guid userId, long membershipGeneration, CancellationToken ct = default)
        => await _db.Roles.AsNoTracking()
            .Include(r => r.Permissions)
            .Where(r => r.ScopeType == scope.Type && r.ScopeId == scope.Id)
            .Where(r => _db.Assignments.Any(a =>
                a.RoleId == r.Id && a.ScopeType == scope.Type && a.ScopeId == scope.Id &&
                a.UserId == userId && a.MembershipGeneration == membershipGeneration))
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

    public async Task<IReadOnlyList<Guid>> ListUserIdsWithRoleAsync(
        AccessScope scope, string normalizedRoleName, long? membershipGeneration, CancellationToken ct = default)
    {
        var query = _db.Assignments.AsNoTracking()
            .Where(a => a.ScopeType == scope.Type && a.ScopeId == scope.Id)
            .Where(a => _db.Roles.Any(r => r.Id == a.RoleId && r.NormalizedName == normalizedRoleName));

        // For the platform scope, only assignments at the member's current generation count, so a
        // deactivated (revoked) member's stale assignments never resurface as an active role holder.
        if (scope.Type == AccessScope.PlatformType)
        {
            query = query.Where(a => _db.PlatformMembers.Any(m =>
                m.PlatformId == scope.Id && m.UserId == a.UserId && m.RevokedAt == null &&
                m.MembershipGeneration == a.MembershipGeneration));
        }
        else if (membershipGeneration is { } generation)
        {
            query = query.Where(a => a.MembershipGeneration == generation);
        }

        return await query.Select(a => a.UserId).Distinct().ToListAsync(ct);
    }

    public void Add(AccessAssignment assignment) => _db.Assignments.Add(assignment);

    public void Remove(AccessAssignment assignment) => _db.Assignments.Remove(assignment);

    public async Task RemoveUserAsync(AccessScope scope, Guid userId, CancellationToken ct = default)
    {
        var assignments = await ListForUserForUpdateAsync(scope, userId, ct);
        if (assignments.Count > 0)
            _db.Assignments.RemoveRange(assignments);
    }
}