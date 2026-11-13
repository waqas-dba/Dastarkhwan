namespace CoreKit.Access.Abstractions;

/// <summary>
/// Platform membership lookup for Access. A user is a platform member only when an
/// AccessPlatformMember row exists with RevokedAt == null. Membership is independent
/// of role assignments: a member with no assignments holds no permissions.
/// </summary>
internal sealed class PlatformAccessMembership : IAccessMembershipAdapter
{
    private readonly AccessDbContext _db;

    public PlatformAccessMembership(AccessDbContext db) => _db = db;

    public Task<AccessMember?> FindAsync(AccessScope scope, Guid userId, CancellationToken ct = default)
    {
        if (scope.Type != AccessScope.PlatformType || userId == Guid.Empty)
            return Task.FromResult<AccessMember?>(null);

        return _db.PlatformMembers
            .Where(m => m.PlatformId == scope.Id && m.UserId == userId && m.RevokedAt == null)
            .Select(m => new AccessMember(false, m.MembershipGeneration))
            .FirstOrDefaultAsync(ct);
    }
}
