using CoreKit.IAM.Common;
using CoreKit.IAM.Interfaces;
using CoreKit.IAM.Models;

using System.Data;


namespace CoreKit.IAM.Services;



public sealed class RoleService : IRoleService
{
    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 500;

    private static readonly string AdministratorNormalized = IamNormalizer.NormalizeName(IamRoleNames.Administrator);

    private readonly IamDbContext _db;

    public RoleService(IamDbContext db) => _db = db;

    public async Task<IamResult<IReadOnlyList<RoleDto>>> ListAsync(CancellationToken ct = default)
    {
        var roles = await ToDtos(_db.Roles.OrderBy(r => r.Name)).ToListAsync(ct);

        return IamResult.Success<IReadOnlyList<RoleDto>>(roles);
    }

    public async Task<IamResult<RoleDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var role = await ToDtos(_db.Roles.Where(r => r.Id == id)).FirstOrDefaultAsync(ct);

        return role is null ? IamErrors.NotFound("Role") : IamResult.Success(role);
    }

    public async Task<IamResult<RoleDto>> CreateAsync(CreateRoleRequest request, CancellationToken ct = default)
    {
        var nameError = ValidateName(request.Name, out var name);

        if (nameError is not null)
            return nameError;

        var descriptionError = ValidateDescription(request.Description, out var description);

        if (descriptionError is not null)
            return descriptionError;

        var permissionIds = (request.PermissionIds ?? new List<Guid>()).Distinct().ToList();

        if (!await AllPermissionsExistAsync(permissionIds, ct))
            return IamErrors.Validation("One or more permissions do not exist.");

        var normalizedName = IamNormalizer.NormalizeName(name!);

        if (await _db.Roles.AnyAsync(r => r.NormalizedName == normalizedName, ct))
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

        _db.Roles.Add(role);

        var saveError = await TrySaveAsync(normalizedName, ct);

        if (saveError is not null)
            return saveError;

        return await GetAsync(role.Id, ct);
    }

    public async Task<IamResult<RoleDto>> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken ct = default)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);

        if (role is null)
            return IamErrors.NotFound("Role");

        if (IsProtected(role))
            return IamErrors.Forbidden("The Administrator role cannot be changed.");

        string? changedName = null;

        if (request.Name is not null)
        {
            var nameError = ValidateName(request.Name, out var name);

            if (nameError is not null)
                return nameError;

            var normalized = IamNormalizer.NormalizeName(name!);

            if (normalized != role.NormalizedName)
            {
                if (await _db.Roles.AnyAsync(r => r.NormalizedName == normalized && r.Id != id, ct))
                    return IamErrors.Conflict("A role with this name already exists.");

                role.NormalizedName = normalized;
                changedName = normalized;
            }

            role.Name = name!;
        }

        if (request.Description is not null)
        {
            var descriptionError = ValidateDescription(request.Description, out var description);

            if (descriptionError is not null)
                return descriptionError;

            role.Description = description;
        }

        if (request.IsActive.HasValue)
            role.IsActive = request.IsActive.Value;

        var saveError = await TrySaveAsync(changedName, ct);

        if (saveError is not null)
            return saveError;

        return await GetAsync(id, ct);
    }

    public async Task<IamResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);

        if (role is null)
            return IamErrors.NotFound("Role");

        if (IsProtected(role))
            return IamErrors.Forbidden("The Administrator role cannot be deleted.");

        var userCount = await _db.UserRoles.CountAsync(ur => ur.RoleId == id, ct);

        if (userCount > 0)
            return IamErrors.Conflict($"The role is assigned to {userCount} user(s). Remove it from them first.");

        _db.Roles.Remove(role); // its permission links are removed by the database (cascade)
        await _db.SaveChangesAsync(ct);

        return IamResult.Success();
    }

    public async Task<IamResult<RoleDto>> SetPermissionsAsync(
        Guid id, SetRolePermissionsRequest request, CancellationToken ct = default)
    {
        var role = await _db.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (role is null)
            return IamErrors.NotFound("Role");

        if (IsProtected(role))
            return IamErrors.Forbidden("The Administrator role always has every permission.");

        var wanted = (request.PermissionIds ?? new List<Guid>()).Distinct().ToList();

        if (!await AllPermissionsExistAsync(wanted, ct))
            return IamErrors.Validation("One or more permissions do not exist.");

        foreach (var link in role.RolePermissions.Where(rp => !wanted.Contains(rp.PermissionId)).ToList())
            _db.RolePermissions.Remove(link);

        var current = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();

        foreach (var permissionId in wanted.Where(p => !current.Contains(p)))
            _db.RolePermissions.Add(new RolePermission { RoleId = id, PermissionId = permissionId });

        await _db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    private static bool IsProtected(Role role) => role.NormalizedName == AdministratorNormalized;

    private static IQueryable<RoleDto> ToDtos(IQueryable<Role> roles) => roles
        .AsNoTracking()
        .Select(r => new RoleDto
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            IsActive = r.IsActive,
            IsProtected = r.NormalizedName == AdministratorNormalized,
            UserCount = r.UserRoles.Count(),
            Permissions = r.RolePermissions.Select(rp => rp.Permission.Name).OrderBy(n => n).ToList()
        });

    private async Task<bool> AllPermissionsExistAsync(List<Guid> permissionIds, CancellationToken ct)
        => permissionIds.Count == 0 ||
           await _db.Permissions.CountAsync(p => permissionIds.Contains(p.Id), ct) == permissionIds.Count;

    private static IamError? ValidateName(string? input, out string? cleaned)
    {
        cleaned = input?.Trim();

        if (string.IsNullOrEmpty(cleaned))
            return IamErrors.Validation("A role name is required.");

        return cleaned.Length > MaxNameLength
            ? IamErrors.Validation($"The role name is too long (max {MaxNameLength} characters).")
            : null;
    }

    private static IamError? ValidateDescription(string? input, out string? cleaned)
    {
        cleaned = string.IsNullOrWhiteSpace(input) ? null : input.Trim();

        return cleaned is { Length: > MaxDescriptionLength }
            ? IamErrors.Validation($"The description is too long (max {MaxDescriptionLength} characters).")
            : null;
    }

    private async Task<IamError?> TrySaveAsync(string? nameToCheck, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateException) when (nameToCheck is not null)
        {
            _db.ChangeTracker.Clear();

            if (await _db.Roles.AnyAsync(r => r.NormalizedName == nameToCheck, ct))
                return IamErrors.Conflict("A role with this name already exists.");

            throw;
        }
    }
}