namespace CoreKit.Access.Services;

internal sealed class AccessAssignmentService : IAccessAssignmentService
{
    private readonly AccessGuard _guard;
    private readonly IAccessMembership _membership;
    private readonly IAccessRoleRepository _roles;
    private readonly IAccessAssignmentRepository _assignments;
    private readonly IAccessAuditRecorder _audit;
    private readonly IAccessUnitOfWork _unitOfWork;
    private readonly IAccessCache _cache;
    private readonly AccessOptions _options;
    private readonly TimeProvider _time;

    public AccessAssignmentService(
        AccessGuard guard,
        IAccessMembership membership,
        IAccessRoleRepository roles,
        IAccessAssignmentRepository assignments,
        IAccessAuditRecorder audit,
        IAccessUnitOfWork unitOfWork,
        IAccessCache cache,
        IOptions<AccessOptions> options,
        TimeProvider time)
    {
        _guard = guard;
        _membership = membership;
        _roles = roles;
        _assignments = assignments;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _options = options.Value;
        _time = time;
    }

    public async Task<AccessResult<IReadOnlyList<AccessRoleDto>>> GetRolesAsync(Guid userId, CancellationToken ct = default)
    {
        var call = await _guard.RequireAsync(AccessPermissionNames.AssignmentsRead, ct);

        if (call.IsFailure)
            return call.Error!;

        if (userId == Guid.Empty)
            return AccessErrors.Validation("A user id is required.");

        var member = await _membership.FindAsync(call.Value.Scope, userId, ct);
        if (member is null)
            return AccessErrors.Validation("The user is not a member of this scope.");

        return AccessResult.Success(await LoadRolesAsync(call.Value.Scope, userId, member.MembershipGeneration, ct));
    }

    public async Task<AccessResult<IReadOnlyList<AccessRoleDto>>> SetRolesAsync(
        Guid userId, SetAccessUserRolesRequest request, CancellationToken ct = default)
    {
        var call = await _guard.RequireAsync(AccessPermissionNames.AssignmentsManage, ct);

        if (call.IsFailure)
            return call.Error!;

        var (scope, actorId, access) = call.Value;

        if (userId == Guid.Empty)
            return AccessErrors.Validation("A user id is required.");

        // Only people who belong to the scope can hold roles in it.
        var member = await _membership.FindAsync(scope, userId, ct);
        if (member is null)
            return AccessErrors.Validation("The user is not a member of this scope.");

        var wanted = (request.RoleIds ?? new List<Guid>()).Where(id => id != Guid.Empty).Distinct().ToList();

        if (wanted.Count > _options.MaxRolesPerUser)
            return AccessErrors.Conflict($"A person can hold at most {_options.MaxRolesPerUser} roles.");

        var wantedRoles = await _roles.FindManyAsync(scope, wanted, ct);

        if (wantedRoles.Count != wanted.Count)
            return AccessErrors.NotFound("One or more roles");

        // Closure slots filled inside the lock.
        if (scope.Type == AccessScope.PlatformType)
        {
            var adminName = AccessValidation.NormalizeName("Administrator");
            var admins = await _assignments.ListUserIdsWithRoleAsync(scope, adminName, null, ct);
            if (admins.Count == 1 && admins[0] == userId && !wantedRoles.Any(r => r.NormalizedName == adminName))
                return AccessErrors.Conflict("The last platform administrator cannot lose the Administrator role.");
        }

        AccessError? earlyError = null;
        IReadOnlyList<AccessRoleDto>? earlyResult = null;

        var lockKey = $"access.assign:{scope.Type}:{scope.Id:N}:{userId:N}";
        var outcome = await _unitOfWork.SaveWithAdvisoryLockAsync(lockKey, async innerCt =>
        {
            // Reset closure slots on every entry: with execution-strategy retries, a failed attempt
            // may have set earlyError/earlyResult before a transient failure, and a subsequent
            // successful retry must start clean.
            earlyError = null;
            earlyResult = null;

            // Re-fetch inside the lock: between the guard check and lock acquisition, the user may
            // have been revoked and re-added, bumping MembershipGeneration. Using a stale generation
            // would write assignments that never become effective.
            var liveMember = await _membership.FindAsync(scope, userId, innerCt);
            if (liveMember is null)
            {
                earlyError = AccessErrors.Validation("The user is no longer a member of this scope.");
                return;
            }
            var generation = liveMember.MembershipGeneration;

            var current = await _assignments.ListForUserForUpdateAsync(scope, userId, innerCt);
            var currentIds = current.Select(a => a.RoleId).ToHashSet();
            var addedIds = wanted.Where(id => !currentIds.Contains(id)).ToList();
            var removedIds = currentIds.Where(id => !wanted.Contains(id)).ToList();
            var needsGenerationRefresh = current.Any(a =>
                wanted.Contains(a.RoleId) && a.MembershipGeneration != generation);

            if (addedIds.Count == 0 && removedIds.Count == 0 && !needsGenerationRefresh)
            {
                earlyResult = await LoadRolesAsync(scope, userId, generation, innerCt);
                return;
            }

            var changedRoleIds = addedIds
                .Concat(removedIds)
                .Concat(current.Where(a => wanted.Contains(a.RoleId)
                                           && a.MembershipGeneration != generation)
                                   .Select(a => a.RoleId))
                .Distinct()
                .ToList();
            var changedRoles = await _roles.FindManyAsync(scope, changedRoleIds, innerCt);
            if (!AccessGuard.Holds(access, changedRoles.SelectMany(r => r.Permissions.Select(p => p.Permission))))
            {
                earlyError = AccessErrors.Escalation;
                return;
            }

            var now = _time.GetUtcNow().UtcDateTime;
            foreach (var assignment in current.Where(a => removedIds.Contains(a.RoleId)))
                _assignments.Remove(assignment);

            foreach (var roleId in wanted)
            {
                var existing = current.FirstOrDefault(a => a.RoleId == roleId);
                if (existing is not null)
                {
                    existing.MembershipGeneration = generation;
                    existing.AssignedAt = now;
                    existing.AssignedBy = actorId;
                }
                else
                {
                    _assignments.Add(new AccessAssignment
                    {
                        RoleId = roleId,
                        UserId = userId,
                        ScopeType = scope.Type,
                        ScopeId = scope.Id,
                        MembershipGeneration = generation,
                        AssignedAt = now,
                        AssignedBy = actorId
                    });
                }
            }

            // The entry says who ends up with which roles, and also exactly what this call added and removed.
            var namesById = changedRoles.ToDictionary(r => r.Id, r => r.Name);

            string NamesOf(IEnumerable<Guid> ids) => string.Join(", ", ids
                .Select(id => namesById.TryGetValue(id, out var name) ? name : id.ToString("N"))
                .Order(StringComparer.Ordinal));

            _audit.Record(scope, AccessAuditActions.RolesAssigned,
                $"user: {userId}; roles: {string.Join(", ", wantedRoles.Select(r => r.Name).Order(StringComparer.Ordinal))}; " +
                $"added: {NamesOf(addedIds)}; removed: {NamesOf(removedIds)}");
        }, ct);

        if (earlyError is not null) return earlyError;
        if (outcome.ToError() is { } saveError) return saveError;
        if (earlyResult is not null) return AccessResult.Success(earlyResult);

_cache.InvalidateUser(scope, userId);
var finalMember = await _membership.FindAsync(scope, userId, ct);
return AccessResult.Success(await LoadRolesAsync(scope, userId, finalMember?.MembershipGeneration ?? member.MembershipGeneration, ct));
    }

    private async Task<IReadOnlyList<AccessRoleDto>> LoadRolesAsync(
        AccessScope scope, Guid userId, long membershipGeneration, CancellationToken ct)
    {
        var roles = await _assignments.ListRolesForUserAsync(scope, userId, membershipGeneration, ct);
        var counts = await _assignments.CountByRoleAsync(scope, ct);

        return roles.Select(r => AccessMapper.ToDto(r, counts.GetValueOrDefault(r.Id))).ToList();
    }
}
