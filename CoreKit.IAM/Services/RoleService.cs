using CoreKit.IAM.Common;
using CoreKit.IAM.Entities;
using CoreKit.IAM.Interfaces;
using CoreKit.IAM.Models;
using CoreKit.IAM.Normalization;
using CoreKit.IAM.Validation;

namespace CoreKit.IAM.Services;

public sealed class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;

    public RoleService(IRoleRepository roleRepository, IPermissionRepository permissionRepository)
    {
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
    }

    public async Task<IamResult<IReadOnlyList<RoleDto>>> ListAsync(CancellationToken ct = default)
        => IamResult.Success(await _roleRepository.ListAsync(ct));

    public async Task<IamResult<RoleDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _roleRepository.GetDetailsAsync(id, ct);

        return role is null ? IamErrors.NotFound("Role") : IamResult.Success(role);
    }

    public async Task<IamResult<RoleDto>> CreateAsync(CreateRoleRequest request, CancellationToken ct = default)
    {
        var nameError = RoleInputValidator.ValidateName(request.Name, out var name);

        if (nameError is not null)
            return nameError;

        var descriptionError = RoleInputValidator.ValidateDescription(request.Description, out var description);

        if (descriptionError is not null)
            return descriptionError;

        var permissionIds = (request.PermissionIds ?? new List<Guid>()).Distinct().ToList();

        if (!await _permissionRepository.AllExistAsync(permissionIds, ct))
            return IamErrors.Validation("One or more permissions do not exist.");

        var normalizedName = IamNormalizer.NormalizeName(name!);

        if (await _roleRepository.NameExistsAsync(normalizedName, ct: ct))
            return IamErrors.Conflict("A role with this name already exists.");

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
            return IamErrors.Conflict("A role with this name already exists.");

        return await GetAsync(role.Id, ct);
    }

    public async Task<IamResult<RoleDto>> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken ct = default)
    {
        var role = await _roleRepository.FindByIdAsync(id, ct);

        if (role is null)
            return IamErrors.NotFound("Role");

        if (AdministratorRole.Is(role))
            return IamErrors.Forbidden("The Administrator role cannot be changed.");

        if (request.Name is not null)
        {
            var nameError = RoleInputValidator.ValidateName(request.Name, out var name);

            if (nameError is not null)
                return nameError;

            var normalized = IamNormalizer.NormalizeName(name!);

            if (normalized != role.NormalizedName)
            {
                if (await _roleRepository.NameExistsAsync(normalized, id, ct))
                    return IamErrors.Conflict("A role with this name already exists.");

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
            return IamErrors.Conflict("A role with this name already exists.");

        return await GetAsync(id, ct);
    }

    public async Task<IamResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _roleRepository.FindByIdAsync(id, ct);

        if (role is null)
            return IamErrors.NotFound("Role");

        if (AdministratorRole.Is(role))
            return IamErrors.Forbidden("The Administrator role cannot be deleted.");

        var userCount = await _roleRepository.CountUsersAsync(id, ct);

        if (userCount > 0)
            return IamErrors.Conflict($"The role is assigned to {userCount} user(s). Remove it from them first.");

        await _roleRepository.DeleteAsync(role, ct);

        return IamResult.Success();
    }

    public async Task<IamResult<RoleDto>> SetPermissionsAsync(
        Guid id, SetRolePermissionsRequest request, CancellationToken ct = default)
    {
        var role = await _roleRepository.FindByIdWithPermissionsAsync(id, ct);

        if (role is null)
            return IamErrors.NotFound("Role");

        if (AdministratorRole.Is(role))
            return IamErrors.Forbidden("The Administrator role always has every permission.");

        var wanted = (request.PermissionIds ?? new List<Guid>()).Distinct().ToList();

        if (!await _permissionRepository.AllExistAsync(wanted, ct))
            return IamErrors.Validation("One or more permissions do not exist.");

        await _roleRepository.ReplacePermissionsAsync(role, wanted, ct);

        return await GetAsync(id, ct);
    }
}