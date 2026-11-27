namespace CoreKit.Access.Services;

/// <summary>
/// Trusted one-time platform provisioning. Bootstraps the initial platform administrator and records a
/// System audit actor whose source identifies the trusted deployment origin. Not exposed to ordinary
/// application callers and not registered in the general DI container; a trusted deployment path
/// composes and invokes it directly.
/// </summary>
public sealed class AccessBootstrap : IAccessBootstrap
{
    private readonly AccessDbContext _db;
    private readonly IAccessRoleRepository _roles;
    private readonly IAccessAssignmentRepository _assignments;
    private readonly IAccessAuditRecorder _audit;
    private readonly IAccessUnitOfWork _unitOfWork;
    private readonly IAccessCache _cache;
    private readonly AccessCatalog _catalog;
    private readonly TimeProvider _time;

    public AccessBootstrap(
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

    public async Task BootstrapInitialPlatformAdminAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            throw new InvalidOperationException("A user id is required to bootstrap a platform administrator.");

        var scope = AccessScope.ForPlatform(AccessScope.PlatformId);

        var adminRoleId = await EnsurePlatformRoleAsync(
            "Administrator",
            "Initial platform administrator",
            new[]
            {
                AccessPermissionNames.RolesRead,
                AccessPermissionNames.RolesManage,
                AccessPermissionNames.AssignmentsRead,
                AccessPermissionNames.AssignmentsManage,
                AccessPermissionNames.AuditRead,
                AccessPermissionNames.PlatformMembersManage
            },
            ct);

        var outcome = await _unitOfWork.SaveWithAdvisoryLockAsync(
            "access.platform.bootstrap", async innerCt =>
            {
                // Re-check inside the lock: another instance may have bootstrapped already.
                var hasOtherAdmin = await _db.Assignments
                    .AnyAsync(a => a.RoleId == adminRoleId && a.UserId != userId, innerCt);
                var alreadyAdmin = await _db.Assignments
                    .AnyAsync(a => a.RoleId == adminRoleId && a.UserId == userId, innerCt);
                if (alreadyAdmin || hasOtherAdmin) return;

                var member = await _db.PlatformMembers
                    .FirstOrDefaultAsync(m => m.UserId == userId && m.PlatformId == AccessScope.PlatformId, innerCt);
                var now = _time.GetUtcNow().UtcDateTime;
                if (member is null)
                {
                    var generation = await NextGenerationAsync(innerCt);
                    member = new AccessPlatformMember
                    {
                        UserId = userId,
                        PlatformId = AccessScope.PlatformId,
                        MembershipGeneration = generation,
                        JoinedAt = now,
                        RevokedAt = null
                    };
                    _db.PlatformMembers.Add(member);
                }
                else if (member.RevokedAt is not null)
                {
                    member.RevokedAt = null;
                    member.MembershipGeneration++;
                    member.JoinedAt = now;
                }

                _assignments.Add(new AccessAssignment
                {
                    RoleId = adminRoleId,
                    UserId = userId,
                    ScopeType = scope.Type,
                    ScopeId = scope.Id,
                    MembershipGeneration = member.MembershipGeneration,
                    AssignedAt = now
                });
                _audit.RecordSystem(scope, "platform.admin.bootstrap", "deployment.bootstrap", $"user: {userId}");
            }, ct);

        EnsureSaved(outcome, "platform admin bootstrap");
        _cache.InvalidateUser(scope, userId);
    }

    /// <summary>
    /// Finds or creates a platform-scope role with this name and sets its permissions to exactly the
    /// given set (adds missing, removes extra). Trusted system provisioning, used to keep the
    /// Administrator role in step with the catalog during bootstrap. Returns the role id.
    /// </summary>
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

    /// <summary>Throws unless the unit of work reports that the change was durably saved, so a failed save
    /// is never mistaken for a successful no-op.</summary>
    private static void EnsureSaved(SaveOutcome outcome, string operation)
    {
        if (outcome != SaveOutcome.Saved)
            throw new InvalidOperationException(
                $"Access {operation} was not persisted: the unit of work reported {outcome}.");
    }

    private async Task<long> NextGenerationAsync(CancellationToken ct)
    {
        var max = await _db.PlatformMembers
            .Where(m => m.PlatformId == AccessScope.PlatformId)
            .MaxAsync(m => (long?)m.MembershipGeneration, ct);
        return (max ?? 0L) + 1L;
    }
}
