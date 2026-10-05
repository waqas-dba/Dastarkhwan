using Iam.Core.Abstractions;
using Iam.Core.Entities;
using Iam.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Iam.Data.Repositories;

public sealed class PermissionRepository : IPermissionRepository
{
    private readonly IamDbContext _db;

    public PermissionRepository(IamDbContext db) => _db = db;

    public Task<Permission?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToLowerInvariant();
        return _db.Permissions.FirstOrDefaultAsync(p => p.Code == normalized, ct);
    }

    public async Task<IReadOnlyList<Permission>> GetByCodesAsync(
        IReadOnlyCollection<string> codes, CancellationToken ct = default)
    {
        var normalized = codes.Select(c => c.Trim().ToLowerInvariant()).Distinct().ToList();

        return await _db.Permissions
            .Where(p => normalized.Contains(p.Code))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken ct = default)
        => await _db.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Code)
            .ToListAsync(ct);

    public void Add(Permission permission) => _db.Permissions.Add(permission);
}