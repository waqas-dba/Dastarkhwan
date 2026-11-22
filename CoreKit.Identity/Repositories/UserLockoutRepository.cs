using CoreKit.Identity.Interfaces;
using CoreKit.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreKit.Identity.Repositories;

public sealed class UserLockoutRepository : IUserLockoutRepository
{
    private readonly IdentityDbContext _db;

    public UserLockoutRepository(IdentityDbContext db) => _db = db;

    public async Task<bool> ClearLockoutAsync(Guid userId, CancellationToken ct = default)
    {
        var affected = await _db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.AccessFailedCount, 0)
                .SetProperty(u => u.LockoutEnd, (DateTime?)null),
                ct);

        return affected > 0;
    }
}
