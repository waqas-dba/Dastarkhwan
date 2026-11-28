namespace CoreKit.Access.Services;

using CoreKit.Access.Persistence;
using CoreKit.Tenant.Abstractions;

internal sealed class AccessMaintenance : IAccessMaintenance
{
    private readonly AccessDbContext _db;
    private readonly IAccessRoleRepository _roles;
    private readonly IAccessAssignmentRepository _assignments;
    private readonly IAccessAuditRecorder _audit;
    private readonly IAccessUnitOfWork _unitOfWork;
    private readonly IAccessCache _cache;
    private readonly AccessCatalog _catalog;
    private readonly TimeProvider _time;
    private readonly ITenantMembershipReader _tenants;

    public AccessMaintenance(
        AccessDbContext db,
        IAccessRoleRepository roles,
        IAccessAssignmentRepository assignments,
        IAccessAuditRecorder audit,
        IAccessUnitOfWork unitOfWork,
        IAccessCache cache,
        AccessCatalog catalog,
        TimeProvider time,
        ITenantMembershipReader tenants)
    {
        _db = db;
        _roles = roles;
        _assignments = assignments;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _catalog = catalog;
        _time = time;
        _tenants = tenants;
    }

    public async Task RemoveUserAsync(AccessScope scope, Guid userId, CancellationToken ct = default)
    {
        if (!scope.IsValid || userId == Guid.Empty)
            return;

        await _assignments.RemoveUserAsync(scope, userId, ct);

        _audit.Record(scope, AccessAuditActions.UserRemoved, $"user: {userId}");

        EnsureSaved(await _unitOfWork.SaveAsync(ct), "user removal");

        // Only invalidate the cache now that the removal and its audit entry are durably persisted.
        _cache.InvalidateUser(scope, userId);
    }

    public async Task RemoveScopeAsync(AccessScope scope, CancellationToken ct = default)
    {
        if (!scope.IsValid)
            return;

        // Purge assignments first so the Restrict FK on AccessAssignment.Role does not block
        // deleting the roles themselves.
        await _assignments.RemoveScopeAsync(scope, ct);

        await _roles.RemoveScopeAsync(scope, ct); // permissions follow (cascade)

        _audit.Record(scope, AccessAuditActions.ScopeRemoved);

        EnsureSaved(await _unitOfWork.SaveAsync(ct), "scope removal");

        // Only invalidate the cache now that the removal and its audit entry are durably persisted.
        _cache.InvalidateScope(scope);
    }

    public async Task ProvisionScopeAsync(AccessScope scope, CancellationToken ct = default)
    {
        if (!scope.IsValid || _catalog.RoleTemplates.Count == 0)
            return;

        // Idempotent: if the scope already has roles, it was provisioned before. Scopes own their
        // roles after creation; template changes affect only newly provisioned scopes.
        var existingCount = await _roles.CountAsync(scope, ct);
        if (existingCount > 0)
            return;

        var templates = _catalog.RoleTemplates
            .Where(t => string.Equals(t.ScopeType, scope.Type, StringComparison.Ordinal))
            .ToList();

        if (templates.Count == 0)
            return;

        var now = _time.GetUtcNow().UtcDateTime;

        foreach (var template in templates)
        {
            var role = new AccessRole
            {
                Id = Guid.NewGuid(),
                ScopeType = scope.Type,
                ScopeId = scope.Id,
                Name = template.Name,
                NormalizedName = AccessValidation.NormalizeName(template.Name),
                Description = template.Description,
                CreatedAt = now,
                ConcurrencyStamp = Guid.NewGuid()
            };

            foreach (var permission in template.Permissions)
                role.Permissions.Add(new AccessRolePermission { RoleId = role.Id, Permission = permission });

            _roles.Add(role);
        }

        _audit.Record(scope, AccessAuditActions.ScopeProvisioned, $"roles: {templates.Count}");

        // Re-read after save to verify provisioning actually persisted (concurrent conflicts).
        var outcome = await _unitOfWork.SaveAsync(ct);

        if (outcome == SaveOutcome.ConstraintViolation)
        {
            // Another instance won the race; verify it actually provisioned roles.
            var after = await _roles.CountAsync(scope, ct);
            if (after > 0) return;
            throw new InvalidOperationException(
                "Access scope provisioning hit a constraint violation but no roles exist.");
        }

        if (outcome != SaveOutcome.Saved)
            throw new InvalidOperationException(
                $"Access scope provisioning was not persisted: the unit of work reported {outcome}.");

        var afterCount = await _roles.CountAsync(scope, ct);
        if (afterCount == 0)
            throw new InvalidOperationException("Access scope provisioning persisted no roles.");
    }

    /// <summary>Throws unless the unit of work reports that the change was durably saved, so a failed save
    /// is never mistaken for a successful no-op.</summary>
    private static void EnsureSaved(SaveOutcome outcome, string operation)
    {
        if (outcome != SaveOutcome.Saved)
            throw new InvalidOperationException(
                $"Access {operation} was not persisted: the unit of work reported {outcome}.");
    }

    public async Task AddPlatformMemberAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return;

        var now = _time.GetUtcNow().UtcDateTime;
        var existing = await _db.PlatformMembers
            .FirstOrDefaultAsync(m => m.UserId == userId && m.PlatformId == AccessScope.PlatformId, ct);

        if (existing is not null)
        {
            if (existing.RevokedAt is null)
                return; // already an active member

            // Reactivate with a new generation so old assignments stay inactive.
            existing.RevokedAt = null;
            existing.MembershipGeneration++;
            existing.JoinedAt = now;
        }
        else
        {
            var generation = await NextGenerationAsync(ct);
            _db.PlatformMembers.Add(new AccessPlatformMember
            {
                UserId = userId,
                PlatformId = AccessScope.PlatformId,
                MembershipGeneration = generation,
                JoinedAt = now,
                RevokedAt = null
            });
        }

        _audit.Record(AccessScope.ForPlatform(AccessScope.PlatformId), AccessAuditActions.MemberAdded, $"user: {userId}");

        EnsureSaved(await _unitOfWork.SaveAsync(ct), "platform member add");

        _cache.InvalidateUser(AccessScope.ForPlatform(AccessScope.PlatformId), userId);
    }

    public async Task RemovePlatformMemberAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return;

        var existing = await _db.PlatformMembers
            .FirstOrDefaultAsync(m => m.UserId == userId && m.PlatformId == AccessScope.PlatformId, ct);

        if (existing is null || existing.RevokedAt is not null)
            return; // already not a member

        existing.RevokedAt = _time.GetUtcNow().UtcDateTime;
        existing.MembershipGeneration++;

        _audit.Record(AccessScope.ForPlatform(AccessScope.PlatformId), AccessAuditActions.MemberRevoked, $"user: {userId}");

        EnsureSaved(await _unitOfWork.SaveAsync(ct), "platform member revoke");

        _cache.InvalidateUser(AccessScope.ForPlatform(AccessScope.PlatformId), userId);
    }

    public async Task SetPlatformUserRolesAsync(
        Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            throw new InvalidOperationException("A user id is required to set platform roles.");

        var scope = AccessScope.ForPlatform(AccessScope.PlatformId);

        var wanted = roleIds.Where(id => id != Guid.Empty).Distinct().ToList();
        var wantedRoles = await _roles.FindManyAsync(scope, wanted, ct);

        if (wantedRoles.Count != wanted.Count)
            throw new InvalidOperationException("One or more roles are not platform-scope roles.");

        var lockKey = $"access.assign:platform:{AccessScope.PlatformId:N}:{userId:N}";
        var outcome = await _unitOfWork.SaveWithAdvisoryLockAsync(lockKey, async innerCt =>
        {
            var member = await _db.PlatformMembers
                .FirstOrDefaultAsync(m => m.UserId == userId && m.PlatformId == AccessScope.PlatformId && m.RevokedAt == null, innerCt);

            if (member is null)
                throw new InvalidOperationException("The user is not an active platform member.");

            var current = await _assignments.ListForUserForUpdateAsync(scope, userId, innerCt);

            foreach (var assignment in current)
                _assignments.Remove(assignment);

            var now = _time.GetUtcNow().UtcDateTime;

            foreach (var roleId in wanted)
            {
                _assignments.Add(new AccessAssignment
                {
                    RoleId = roleId,
                    UserId = userId,
                    ScopeType = scope.Type,
                    ScopeId = scope.Id,
                    MembershipGeneration = member.MembershipGeneration,
                    AssignedAt = now
                });
            }

            _audit.Record(
                scope,
                AccessAuditActions.RolesAssigned,
                $"user: {userId}; roles: {string.Join(", ", wantedRoles.Select(r => r.Name).Order(StringComparer.Ordinal))}");
        }, ct);

        if (outcome != SaveOutcome.Saved)
            throw new InvalidOperationException(
                $"Access platform role assignment was not persisted: the unit of work reported {outcome}.");

        _cache.InvalidateUser(scope, userId);
    }

public async Task RemoveUserFromAllScopesAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return;

        // Get all tenant IDs from the tenant module's membership reader (the authoritative source;
        // Access holds no membership projection of its own).
        var memberships = await _tenants.ListForUserIncludingRemovedAsync(userId, ct);

        // Remove from all tenant scopes
        // Source of truth for "where does this user hold roles" is Access itself, not Tenant
        // (Tenant memberships are already hard-deleted by the time this handler runs).
        var scopes = (await _assignments.ListScopesForUserAsync(userId, ct)).ToHashSet();
        foreach (var (tenantId, _, _) in memberships)
            scopes.Add(AccessScope.ForTenant(tenantId));
        scopes.Remove(AccessScope.ForPlatform(AccessScope.PlatformId));
        foreach (var scope in scopes)
            _audit.Record(scope, AccessAuditActions.UserRemoved, $"user: {userId}; account-deleted");
        await _assignments.RemoveUserEverywhereAsync(userId, ct);

        // Remove platform membership
        var platformScope = AccessScope.ForPlatform(AccessScope.PlatformId);

        // Remove platform assignments first so the Restrict FK doesn't block anything.
        await _assignments.RemoveUserAsync(platformScope, userId, ct);

        var platformMember = await _db.PlatformMembers
            .FirstOrDefaultAsync(m => m.UserId == userId && m.PlatformId == AccessScope.PlatformId, ct);

        if (platformMember is not null && platformMember.RevokedAt is null)
        {
            platformMember.RevokedAt = _time.GetUtcNow().UtcDateTime;
            platformMember.MembershipGeneration++;
            _audit.Record(platformScope, AccessAuditActions.MemberRevoked, $"user: {userId}; account-deleted");
        }

        EnsureSaved(await _unitOfWork.SaveAsync(ct), "remove user from all scopes");

        // Invalidate cache for all affected scopes
        foreach (var s in scopes)
            _cache.InvalidateUser(s, userId);
        _cache.InvalidateUser(platformScope, userId);
    }

    private async Task<long> NextGenerationAsync(CancellationToken ct)
    {
        var max = await _db.PlatformMembers
            .Where(m => m.PlatformId == AccessScope.PlatformId)
            .MaxAsync(m => (long?)m.MembershipGeneration, ct);
        return (max ?? 0L) + 1L;
    }
}