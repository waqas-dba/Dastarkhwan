using Iam.Core.Abstractions;
using Iam.Core.Entities;
using Iam.Core.Identity;
using Iam.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Iam.Data.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly IamDbContext _db;

    public UserRepository(IamDbContext db) => _db = db;

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> GetByLoginAsync(LoginIdentifier identifier, CancellationToken ct = default)
    {
        var value = identifier.NormalizedValue;
        var query = _db.Users.Include(u => u.UserRoles);

        return identifier.Kind == LoginIdentifierKind.Email
            ? await query.FirstOrDefaultAsync(u => u.Email == value, ct)
            : await query.FirstOrDefaultAsync(u => u.Phone == value, ct);
    }

    public Task<bool> LoginExistsAsync(LoginIdentifier identifier, CancellationToken ct = default)
    {
        var value = identifier.NormalizedValue;

        return identifier.Kind == LoginIdentifierKind.Email
            ? _db.Users.AnyAsync(u => u.Email == value, ct)
            : _db.Users.AnyAsync(u => u.Phone == value, ct);
    }

    public async Task<IReadOnlyList<string>> GetPermissionCodesAsync(Guid userId, CancellationToken ct = default)
    {
        var codes = await (
                from userRole in _db.UserRoles
                where userRole.UserId == userId
                join rolePermission in _db.RolePermissions on userRole.RoleId equals rolePermission.RoleId
                join permission in _db.Permissions on rolePermission.PermissionId equals permission.Id
                select permission.Code)
            .Distinct()
            .ToListAsync(ct);

        return codes;
    }

    public void Add(User user) => _db.Users.Add(user);
}