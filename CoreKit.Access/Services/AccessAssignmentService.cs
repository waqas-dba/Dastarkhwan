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

        return AccessResult.Success(await LoadRolesAsync(call.Value.Scope, userId, ct));
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
        if (await _membership.FindAsync(scope, userId, ct) is null)
            return AccessErrors.Validation("The user is not a member of this scope.");

        var wanted = (request.RoleIds ?? new List<Guid>()).Where(id => id != Guid.Empty).Distinct().ToList();

        if (wanted.Count > _options.MaxRolesPerUser)
            return AccessErrors.Conflict($"A person can hold at most {_options.MaxRolesPerUser} roles.");

        var wantedRoles = await _roles.FindManyAsync(scope, wanted, ct);

        if (wantedRoles.Count != wanted.Count)
            return AccessErrors.NotFound("One or more roles");

        var current = await _assignments.ListForUserForUpdateAsync(scope, userId, ct);
        var currentIds = current.Select(a => a.RoleId).ToHashSet();

        var addedIds = wanted.Where(id => !currentIds.Contains(id)).ToList();
        var removedIds = currentIds.Where(id => !wanted.Contains(id)).ToList();

        if (addedIds.Count == 0 && removedIds.Count == 0)
            return AccessResult.Success(await LoadRolesAsync(scope, userId, ct));

        // You can only give or take away roles whose permissions you hold, so nobody can promote
        // themselves or someone else past their own level, or strip someone more powerful.
        var changedRoles = await _roles.FindManyAsync(scope, addedIds.Concat(removedIds).ToList(), ct);

        if (!AccessGuard.Holds(access, changedRoles.SelectMany(r => r.Permissions.Select(p => p.Permission))))
            return AccessErrors.Escalation;

        var now = _time.GetUtcNow().UtcDateTime;

        foreach (var assignment in current.Where(a => removedIds.Contains(a.RoleId)))
            _assignments.Remove(assignment);

        foreach (var roleId in addedIds)
            _assignments.Add(new AccessAssignment
            {
                RoleId = roleId,
                UserId = userId,
                ScopeType = scope.Type,
                ScopeId = scope.Id,
                AssignedAt = now,
                AssignedBy = actorId
            });

        _audit.Record(
            scope,
            AccessAuditActions.RolesAssigned,
            $"user: {userId}; roles: {string.Join(", ", wantedRoles.Select(r => r.Name).Order(StringComparer.Ordinal))}");

        if ((await _unitOfWork.SaveAsync(ct)).ToError() is { } saveError)
            return saveError;

        _cache.InvalidateUser(scope, userId);

        return AccessResult.Success(await LoadRolesAsync(scope, userId, ct));
    }

    private async Task<IReadOnlyList<AccessRoleDto>> LoadRolesAsync(AccessScope scope, Guid userId, CancellationToken ct)
    {
        var roles = await _assignments.ListRolesForUserAsync(scope, userId, ct);
        var counts = await _assignments.CountByRoleAsync(scope, ct);

        return roles.Select(r => AccessMapper.ToDto(r, counts.GetValueOrDefault(r.Id))).ToList();
    }
}