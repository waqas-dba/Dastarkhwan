namespace CoreKit.Access.Services;

internal sealed class AccessRoleService : IAccessRoleService
{
    private readonly AccessGuard _guard;
    private readonly IAccessRoleRepository _roles;
    private readonly IAccessAssignmentRepository _assignments;
    private readonly IAccessAuditRecorder _audit;
    private readonly IAccessUnitOfWork _unitOfWork;
    private readonly IAccessCache _cache;
    private readonly AccessCatalog _catalog;
    private readonly AccessOptions _options;
    private readonly TimeProvider _time;

    public AccessRoleService(
        AccessGuard guard,
        IAccessRoleRepository roles,
        IAccessAssignmentRepository assignments,
        IAccessAuditRecorder audit,
        IAccessUnitOfWork unitOfWork,
        IAccessCache cache,
        AccessCatalog catalog,
        IOptions<AccessOptions> options,
        TimeProvider time)
    {
        _guard = guard;
        _roles = roles;
        _assignments = assignments;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _catalog = catalog;
        _options = options.Value;
        _time = time;
    }

    public async Task<AccessResult<IReadOnlyList<AccessPermissionDto>>> ListPermissionsAsync(CancellationToken ct = default)
    {
        var call = await _guard.RequireAsync(AccessPermissionNames.RolesRead, ct);

        if (call.IsFailure)
            return call.Error!;

        return AccessResult.Success<IReadOnlyList<AccessPermissionDto>>(
            _catalog.Permissions.Select(p => new AccessPermissionDto { Name = p.Name, Description = p.Description }).ToList());
    }

    public async Task<AccessResult<IReadOnlyList<AccessRoleDto>>> ListAsync(CancellationToken ct = default)
    {
        var call = await _guard.RequireAsync(AccessPermissionNames.RolesRead, ct);

        if (call.IsFailure)
            return call.Error!;

        var scope = call.Value.Scope;
        var roles = await _roles.ListAsync(scope, ct);
        var counts = await _assignments.CountByRoleAsync(scope, ct);

        return AccessResult.Success<IReadOnlyList<AccessRoleDto>>(
            roles.Select(r => AccessMapper.ToDto(r, counts.GetValueOrDefault(r.Id))).ToList());
    }

    public async Task<AccessResult<AccessRoleDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var call = await _guard.RequireAsync(AccessPermissionNames.RolesRead, ct);

        if (call.IsFailure)
            return call.Error!;

        var role = await _roles.FindAsync(call.Value.Scope, id, ct);

        if (role is null)
            return AccessErrors.NotFound("Role");

        return AccessResult.Success(AccessMapper.ToDto(role, await _assignments.CountForRoleAsync(id, ct)));
    }

    public async Task<AccessResult<AccessRoleDto>> CreateAsync(CreateAccessRoleRequest request, CancellationToken ct = default)
    {
        var call = await _guard.RequireAsync(AccessPermissionNames.RolesManage, ct);

        if (call.IsFailure)
            return call.Error!;

        var (scope, _, access) = call.Value;

        var nameError = AccessValidation.ValidateName(request.Name, out var name);

        if (nameError is not null)
            return nameError;

        var descriptionError = AccessValidation.ValidateDescription(request.Description, out var description);

        if (descriptionError is not null)
            return descriptionError;

        var permissionError = AccessValidation.ValidatePermissions(request.Permissions, _catalog, out var permissions);

        if (permissionError is not null)
            return permissionError;

        if (!AccessGuard.Holds(access, permissions))
            return AccessErrors.Escalation;

        if (await _roles.CountAsync(scope, ct) >= _options.MaxRolesPerScope)
            return AccessErrors.Conflict($"At most {_options.MaxRolesPerScope} roles are allowed.");

        var normalizedName = AccessValidation.NormalizeName(name!);

        if (await _roles.NameExistsAsync(scope, normalizedName, null, ct))
            return AccessErrors.Conflict("A role with this name already exists.");

        var role = new AccessRole
        {
            Id = Guid.NewGuid(),
            ScopeType = scope.Type,
            ScopeId = scope.Id,
            Name = name!,
            NormalizedName = normalizedName,
            Description = description,
            CreatedAt = Now(),
            ConcurrencyStamp = Guid.NewGuid()
        };

        foreach (var permission in permissions)
            role.Permissions.Add(new AccessRolePermission { RoleId = role.Id, Permission = permission });

        _roles.Add(role);
        _audit.Record(scope, AccessAuditActions.RoleCreated, $"role: {role.Name}; permissions: {permissions.Count}");

        if ((await _unitOfWork.SaveAsync(ct)).ToError() is { } saveError)
            return saveError;

        return AccessResult.Success(AccessMapper.ToDto(role, 0));
    }

    public async Task<AccessResult<AccessRoleDto>> UpdateAsync(Guid id, UpdateAccessRoleRequest request, CancellationToken ct = default)
    {
        var call = await _guard.RequireAsync(AccessPermissionNames.RolesManage, ct);

        if (call.IsFailure)
            return call.Error!;

        var (scope, _, access) = call.Value;

        // Validate first, then change, so a refused request leaves nothing half-done.
        string? newName = null;

        if (request.Name is not null)
        {
            var nameError = AccessValidation.ValidateName(request.Name, out newName);

            if (nameError is not null)
                return nameError;
        }

        string? newDescription = null;

        if (request.Description is not null)
        {
            var descriptionError = AccessValidation.ValidateDescription(request.Description, out newDescription);

            if (descriptionError is not null)
                return descriptionError;
        }

        var role = await _roles.FindForUpdateAsync(scope, id, ct);

        if (role is null)
            return AccessErrors.NotFound("Role");

        // A role stronger than you cannot be edited by you.
        if (!AccessGuard.Holds(access, role.Permissions.Select(p => p.Permission)))
            return AccessErrors.Escalation;

        var changes = new List<string>();

        if (newName is not null && newName != role.Name)
        {
            var normalized = AccessValidation.NormalizeName(newName);

            if (normalized != role.NormalizedName && await _roles.NameExistsAsync(scope, normalized, id, ct))
                return AccessErrors.Conflict("A role with this name already exists.");

            changes.Add($"name: {role.Name} -> {newName}");
        }

        if (request.Description is not null && newDescription != role.Description)
            changes.Add("description changed");

        var memberCount = await _assignments.CountForRoleAsync(id, ct);

        if (changes.Count == 0)
            return AccessResult.Success(AccessMapper.ToDto(role, memberCount));

        if (newName is not null)
        {
            role.Name = newName;
            role.NormalizedName = AccessValidation.NormalizeName(newName);
        }

        if (request.Description is not null)
            role.Description = newDescription;

        role.UpdatedAt = Now();
        role.ConcurrencyStamp = Guid.NewGuid();

        _audit.Record(scope, AccessAuditActions.RoleUpdated, $"role: {role.Name}; " + string.Join("; ", changes));

        if ((await _unitOfWork.SaveAsync(ct)).ToError() is { } saveError)
            return saveError;

        _cache.InvalidateScope(scope);

        return AccessResult.Success(AccessMapper.ToDto(role, memberCount));
    }

    public async Task<AccessResult<AccessRoleDto>> SetPermissionsAsync(
        Guid id, SetAccessRolePermissionsRequest request, CancellationToken ct = default)
    {
        var call = await _guard.RequireAsync(AccessPermissionNames.RolesManage, ct);

        if (call.IsFailure)
            return call.Error!;

        var (scope, _, access) = call.Value;

        var permissionError = AccessValidation.ValidatePermissions(request.Permissions, _catalog, out var wanted);

        if (permissionError is not null)
            return permissionError;

        var role = await _roles.FindForUpdateAsync(scope, id, ct);

        if (role is null)
            return AccessErrors.NotFound("Role");

        var current = role.Permissions.Select(p => p.Permission).ToList();

        // You must hold everything the role has now and everything it will have, so you can neither
        // hand out a permission you lack nor strip a role that is stronger than you.
        if (!AccessGuard.Holds(access, current.Concat(wanted)))
            return AccessErrors.Escalation;

        var memberCount = await _assignments.CountForRoleAsync(id, ct);
        var added = wanted.Except(current).ToList();
        var removed = current.Except(wanted).ToList();

        if (added.Count == 0 && removed.Count == 0)
            return AccessResult.Success(AccessMapper.ToDto(role, memberCount));

        foreach (var link in role.Permissions.Where(p => removed.Contains(p.Permission)).ToList())
            role.Permissions.Remove(link);

        foreach (var permission in added)
            role.Permissions.Add(new AccessRolePermission { RoleId = role.Id, Permission = permission });

        role.UpdatedAt = Now();
        role.ConcurrencyStamp = Guid.NewGuid();

        _audit.Record(
            scope,
            AccessAuditActions.RolePermissionsChanged,
            $"role: {role.Name}; added: {string.Join(", ", added)}; removed: {string.Join(", ", removed)}");

        if ((await _unitOfWork.SaveAsync(ct)).ToError() is { } saveError)
            return saveError;

        _cache.InvalidateScope(scope);

        return AccessResult.Success(AccessMapper.ToDto(role, memberCount));
    }

    public async Task<AccessResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var call = await _guard.RequireAsync(AccessPermissionNames.RolesManage, ct);

        if (call.IsFailure)
            return call.Error!;

        var (scope, _, access) = call.Value;

        var role = await _roles.FindForUpdateAsync(scope, id, ct);

        if (role is null)
            return AccessErrors.NotFound("Role");

        if (!AccessGuard.Holds(access, role.Permissions.Select(p => p.Permission)))
            return AccessErrors.Escalation;

        var memberCount = await _assignments.CountForRoleAsync(id, ct);

        if (memberCount > 0)
            return AccessErrors.Conflict($"The role is held by {memberCount} person(s). Remove it from them first.");

        _roles.Remove(role);
        _audit.Record(scope, AccessAuditActions.RoleDeleted, $"role: {role.Name}");

        if ((await _unitOfWork.SaveAsync(ct)).ToError() is { } saveError)
            return saveError;

        return AccessResult.Success();
    }

    private DateTime Now() => _time.GetUtcNow().UtcDateTime;
}