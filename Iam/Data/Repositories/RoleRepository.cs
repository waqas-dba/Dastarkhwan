using Iam.Core.Abstractions;
using Iam.Core.Entities;
using Iam.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Iam.Data.Repositories;

public sealed class RoleRepository : IRoleRepository
{
    private readonly IamDbContext _db;

    public RoleRepository(IamDbContext db) => _db = db;

    public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<Role?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var normalized = name.Trim().ToUpperInvariant();

        return _db.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.NormalizedName == normalized, ct);
    }

    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default)
        => await _db.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Role>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => await _db.Roles
            .Where(r => ids.Contains(r.Id))
            .ToListAsync(ct);

    public Task<bool> IsAssignedToAnyUserAsync(Guid roleId, CancellationToken ct = default)
        => _db.UserRoles.AnyAsync(ur => ur.RoleId == roleId, ct);

    public void Add(Role role) => _db.Roles.Add(role);

    public void Remove(Role role) => _db.Roles.Remove(role);
}