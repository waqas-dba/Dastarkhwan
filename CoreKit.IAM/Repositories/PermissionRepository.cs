using CoreKit.IAM.Interfaces;
using CoreKit.IAM.Models;
using CoreKit.IAM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreKit.IAM.Repositories;

public sealed class PermissionRepository : IPermissionRepository
{
    private readonly IamDbContext _db;

    public PermissionRepository(IamDbContext db) => _db = db;

    public async Task<IReadOnlyList<PermissionDto>> ListAsync(CancellationToken ct = default)
        => await _db.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new PermissionDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                IsActive = p.IsActive
            })
            .ToListAsync(ct);

    public Task<PermissionDto?> FindAsync(Guid id, CancellationToken ct = default)
        => _db.Permissions
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new PermissionDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                IsActive = p.IsActive
            })
            .FirstOrDefaultAsync(ct);

    public async Task<bool> AllExistAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => ids.Count == 0 ||
           await _db.Permissions.CountAsync(p => ids.Contains(p.Id), ct) == ids.Count;

    public async Task<IReadOnlyList<string>> GetActiveNamesForRolesAsync(
        IReadOnlyCollection<Guid> roleIds, CancellationToken ct = default)
        => await _db.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId) && rp.Permission.IsActive)
            .Select(rp => rp.Permission.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(ct);
}