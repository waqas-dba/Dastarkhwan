using CoreKit.IAM.Common;
using CoreKit.IAM.Interfaces;
using CoreKit.IAM.Models;


namespace CoreKit.IAM.Services;

public sealed class PermissionService : IPermissionService
{
    private readonly IamDbContext _db;

    public PermissionService(IamDbContext db) => _db = db;

    public async Task<IamResult<IReadOnlyList<PermissionDto>>> ListAsync(CancellationToken ct = default)
    {
        var permissions = await _db.Permissions
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

        return IamResult.Success<IReadOnlyList<PermissionDto>>(permissions);
    }

    public async Task<IamResult<PermissionDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var permission = await _db.Permissions
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

        return permission is null ? IamErrors.NotFound("Permission") : IamResult.Success(permission);
    }
}
