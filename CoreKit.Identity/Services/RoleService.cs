using CoreKit.Identity.Common;
using CoreKit.Identity.Entities;
using CoreKit.Identity.Interfaces;
using CoreKit.Identity.Models;
using CoreKit.Identity.Normalization;
using CoreKit.Identity.Validation;

namespace CoreKit.Identity.Services;

public sealed class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;

    public RoleService(IRoleRepository roleRepository, IPermissionRepository permissionRepository)
    {
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
    }

    public async Task<IdentityResult<IReadOnlyList<RoleDto>>> ListAsync(CancellationToken ct = default)
        => Common.IdentityResult.Success(await _roleRepository.ListAsync(ct));

    public async Task<IdentityResult<RoleDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _roleRepository.GetDetailsAsync(id, ct);

        return role is null ? IdentityErrors.NotFound("Role") : Common.IdentityResult.Success(role);
    }

    public async Task<IdentityResult<RoleDto>> CreateAsync(CreateRoleRequest request, CancellationToken ct = default)
    {
        var nameError = RoleInputValidator.ValidateName(request.Name, out var name);

        if (nameError is not null)
            return nameError;

        var descriptionError = RoleInputValidator.ValidateDescription(request.Description, out var description);

        if (descriptionError is not null)
            return descriptionError;

        var permissionIds = (request.PermissionIds ?? new List<Guid>()).Distinct().ToList();

        if (!await _permissionRepository.AllExistAsync(permissionIds, ct))
            return IdentityErrors.Validation("One or more permissions do not exist.");

        var normalizedName = IdentityNormalizer.NormalizeName(name!);

        if (await _roleRepository.NameExistsAsync(normalizedName, ct: ct))
            return IdentityErrors.Conflict("A role with this name already exists.");

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = name!,
            NormalizedName = normalizedName,
            Description = description,
            IsActive = request.IsActive
        };

        foreach (var permissionId in permissionIds)
            role.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionId });

        if (!await _roleRepository.CreateAsync(role, ct))
            return IdentityErrors.Conflict("A role with this name already exists.");

        return await GetAsync(role.Id, ct);
    }

    public async Task<IdentityResult<RoleDto>> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken ct = default)
    {
        var role = await _roleRepository.FindByIdAsync(id, ct);

        if (role is null)
            return IdentityErrors.NotFound("Role");

        if (AdministratorRole.Is(role))
            return IdentityErrors.Forbidden("The Administrator role cannot be changed.");

        if (request.Name is not null)
        {
            var nameError = RoleInputValidator.ValidateName(request.Name, out var name);

            if (nameError is not null)
                return nameError;

            var normalized = IdentityNormalizer.NormalizeName(name!);

            if (normalized != role.NormalizedName)
            {
                if (await _roleRepository.NameExistsAsync(normalized, id, ct))
                    return IdentityErrors.Conflict("A role with this name already exists.");

                role.NormalizedName = normalized;
            }

            role.Name = name!;
        }

        if (request.Description is not null)
        {
            var descriptionError = RoleInputValidator.ValidateDescription(request.Description, out var description);

            if (descriptionError is not null)
                return descriptionError;

            role.Description = description;
        }

        if (request.IsActive.HasValue)
            role.IsActive = request.IsActive.Value;

        if (!await _roleRepository.UpdateAsync(role, ct))
            return IdentityErrors.Conflict("A role with this name already exists.");

        return await GetAsync(id, ct);
    }

    public async Task<Common.IdentityResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _roleRepository.FindByIdAsync(id, ct);

        if (role is null)
            return IdentityErrors.NotFound("Role");

        if (AdministratorRole.Is(role))
            return IdentityErrors.Forbidden("The Administrator role cannot be deleted.");

        var userCount = await _roleRepository.CountUsersAsync(id, ct);

        if (userCount > 0)
            return IdentityErrors.Conflict($"The role is assigned to {userCount} user(s). Remove it from them first.");

        await _roleRepository.DeleteAsync(role, ct);

        return Common.IdentityResult.Success();
    }

    public async Task<IdentityResult<RoleDto>> SetPermissionsAsync(
        Guid id, SetRolePermissionsRequest request, CancellationToken ct = default)
    {
        var role = await _roleRepository.FindByIdWithPermissionsAsync(id, ct);

        if (role is null)
            return IdentityErrors.NotFound("Role");

        if (AdministratorRole.Is(role))
            return IdentityErrors.Forbidden("The Administrator role always has every permission.");

        var wanted = (request.PermissionIds ?? new List<Guid>()).Distinct().ToList();

        if (!await _permissionRepository.AllExistAsync(wanted, ct))
            return IdentityErrors.Validation("One or more permissions do not exist.");

        await _roleRepository.ReplacePermissionsAsync(role, wanted, ct);

        return await GetAsync(id, ct);
    }
}