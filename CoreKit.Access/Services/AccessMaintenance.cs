namespace CoreKit.Access.Services;

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

    public AccessMaintenance(
        AccessDbContext db,
        IAccessRoleRepository roles,
        IAccessAssignmentRepository assignments,
        IAccessAuditRecorder audit,
        IAccessUnitOfWork unitOfWork,
        IAccessCache cache,
        AccessCatalog catalog,
        TimeProvider time)
    {
        _db = db;
        _roles = roles;
        _assignments = assignments;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _catalog = catalog;
        _time = time;
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

        await _roles.RemoveScopeAsync(scope, ct); // permissions and assignments follow (cascade)

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
        if (outcome != SaveOutcome.Saved)
            throw new InvalidOperationException(
                $"Access scope provisioning was not persisted: the unit of work reported {outcome}.");

        // Verify: another concurrent provisioning may have added roles between our check and save.
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

        var member = await _db.PlatformMembers
            .FirstOrDefaultAsync(m => m.UserId == userId && m.PlatformId == AccessScope.PlatformId && m.RevokedAt == null, ct);

        if (member is null)
            throw new InvalidOperationException("The user is not an active platform member.");

        var wanted = roleIds.Where(id => id != Guid.Empty).Distinct().ToList();
        var wantedRoles = await _roles.FindManyAsync(scope, wanted, ct);

        if (wantedRoles.Count != wanted.Count)
            throw new InvalidOperationException("One or more roles are not platform-scope roles.");

        var current = await _assignments.ListForUserForUpdateAsync(scope, userId, ct);

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

        EnsureSaved(await _unitOfWork.SaveAsync(ct), "platform role assignment");

        _cache.InvalidateUser(scope, userId);
    }

    private async Task<long> NextGenerationAsync(CancellationToken ct)
    {
        var max = await _db.PlatformMembers
            .Where(m => m.PlatformId == AccessScope.PlatformId)
            .MaxAsync(m => (long?)m.MembershipGeneration, ct);
        return (max ?? 0L) + 1L;
    }

    public async Task<Guid> EnsurePlatformRoleAsync(
        string name, string? description, IReadOnlyCollection<string> permissions, CancellationToken ct = default)
    {
        var scope = AccessScope.ForPlatform(AccessScope.PlatformId);
        var normalized = AccessValidation.NormalizeName(name);

        var existingId = (await _roles.ListAsync(scope, ct))
            .FirstOrDefault(r => r.NormalizedName == normalized)?.Id;

        AccessRole role;

        if (existingId is { } id)
        {
            role = await _roles.FindForUpdateAsync(scope, id, ct)
                ?? throw new InvalidOperationException("Platform role disappeared during seeding.");
            role.Description = description;
        }
        else
        {
            role = new AccessRole
            {
                Id = Guid.NewGuid(),
                ScopeType = scope.Type,
                ScopeId = scope.Id,
                Name = name,
                NormalizedName = normalized,
                Description = description,
                CreatedAt = _time.GetUtcNow().UtcDateTime,
                ConcurrencyStamp = Guid.NewGuid()
            };
            _roles.Add(role);
        }

        var wanted = permissions
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

        foreach (var link in role.Permissions.Where(p => !wanted.Contains(p.Permission)).ToList())
            role.Permissions.Remove(link);

        var current = role.Permissions.Select(p => p.Permission).ToHashSet(StringComparer.Ordinal);

        foreach (var permission in wanted.Where(p => !current.Contains(p)))
            role.Permissions.Add(new AccessRolePermission { RoleId = role.Id, Permission = permission });

        EnsureSaved(await _unitOfWork.SaveAsync(ct), "platform role ensure");

        _cache.InvalidateScope(scope);

        return role.Id;
    }
}