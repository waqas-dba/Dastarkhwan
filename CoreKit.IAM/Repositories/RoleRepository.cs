using CoreKit.IAM.Common;
using CoreKit.IAM.Entities;
using CoreKit.IAM.Interfaces;
using CoreKit.IAM.Models;
using CoreKit.IAM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreKit.IAM.Repositories;

internal sealed class RoleRepository : IRoleRepository
{
    private readonly IamDbContext _db;

    public RoleRepository(IamDbContext db) => _db = db;

    public async Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken ct = default)
        => await ToDtos(_db.Roles.OrderBy(r => r.Name)).ToListAsync(ct);

    public Task<RoleDto?> GetDetailsAsync(Guid id, CancellationToken ct = default)
        => ToDtos(_db.Roles.Where(r => r.Id == id)).FirstOrDefaultAsync(ct);

    public Task<Role?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<Role?> FindByIdWithPermissionsAsync(Guid id, CancellationToken ct = default)
        => _db.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<Role>> FindByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return new List<Role>();

        return await _db.Roles
            .AsNoTracking()
            .Where(r => ids.Contains(r.Id))
            .ToListAsync(ct);
    }

    public Task<bool> NameExistsAsync(string normalizedName, Guid? excludingRoleId = null, CancellationToken ct = default)
        => _db.Roles.AnyAsync(
            r => r.NormalizedName == normalizedName &&
                 (excludingRoleId == null || r.Id != excludingRoleId.Value),
            ct);

    public Task<int> CountUsersAsync(Guid roleId, CancellationToken ct = default)
        => _db.UserRoles.CountAsync(ur => ur.RoleId == roleId, ct);

    public async Task<bool> CreateAsync(Role role, CancellationToken ct = default)
    {
        _db.Roles.Add(role);
        return await TrySaveAsync(role, ct);
    }

    public Task<bool> UpdateAsync(Role role, CancellationToken ct = default)
        => TrySaveAsync(role, ct);

    public async Task DeleteAsync(Role role, CancellationToken ct = default)
    {
        _db.Roles.Remove(role); // its permission links are removed by the database (cascade)
        await _db.SaveChangesAsync(ct);
    }

    public async Task ReplacePermissionsAsync(
        Role role, IReadOnlyCollection<Guid> permissionIds, CancellationToken ct = default)
    {
        foreach (var link in role.RolePermissions.Where(rp => !permissionIds.Contains(rp.PermissionId)).ToList())
            _db.RolePermissions.Remove(link);

        var current = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();

        foreach (var permissionId in permissionIds.Where(p => !current.Contains(p)))
            _db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionId });

        await _db.SaveChangesAsync(ct);
    }

    private static IQueryable<RoleDto> ToDtos(IQueryable<Role> roles) => roles
        .AsNoTracking()
        .Select(r => new RoleDto
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            IsActive = r.IsActive,
            IsProtected = r.NormalizedName == AdministratorRole.NormalizedName,
            UserCount = r.UserRoles.Count(),
            Permissions = r.RolePermissions.Select(rp => rp.Permission.Name).OrderBy(n => n).ToList()
        });

    private async Task<bool> TrySaveAsync(Role role, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            var normalizedName = role.NormalizedName;
            var roleId = role.Id;

            _db.ChangeTracker.Clear();

            if (await _db.Roles.AnyAsync(r => r.NormalizedName == normalizedName && r.Id != roleId, ct))
                return false;

            throw;
        }
    }
}