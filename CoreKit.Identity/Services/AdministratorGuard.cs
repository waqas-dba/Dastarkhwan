using CoreKit.Identity.Contracts;
using CoreKit.Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CoreKit.Identity.Services;

public sealed class AdministratorGuard : IAdministratorGuard
{
    private readonly IPlatformAdministratorProvider _provider;
    private readonly IdentityDbContext _db;
    // In-process mutex serializing last-administrator checks so concurrent requests
    // cannot both decide it is safe to deactivate the last active administrator.
    // Cross-instance serialization is provided by the database-level advisory lock
    // used by the Access unit of work; on non-PostgreSQL providers this guard is
    // single-instance only, backed by the SecurityVersion check in TryDeactivateAsync.
    private static readonly SemaphoreSlim s_adminCheckLock = new(1, 1);

    public AdministratorGuard(IPlatformAdministratorProvider provider,
                              IUserRepository userRepository,
                              IdentityDbContext db)
    {
        _provider = provider;
        _db = db;
    }

    public async Task<bool> IsLastActiveAdministratorAsync(Guid userId, CancellationToken ct = default)
    {
        await s_adminCheckLock.WaitAsync(ct);
        try
        {
            return await CheckAsync(userId, ct);
        }
        finally
        {
            s_adminCheckLock.Release();
        }
    }

    private async Task<bool> CheckAsync(Guid userId, CancellationToken ct)
    {
        var ownsTransaction = _db.Database.CurrentTransaction is null;
        IDbContextTransaction? transaction = null;

        if (ownsTransaction)
            transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            var adminUserIds = await _provider.GetPlatformAdministratorUserIdsAsync(ct);
            if (adminUserIds.Count == 0)
                return false;

            var activeAdminIds = await _db.Users
                .Where(u => adminUserIds.Contains(u.Id) && u.IsActive)
                .Select(u => u.Id)
                .ToListAsync(ct);

            if (activeAdminIds.Count != 1)
                return false;

            return activeAdminIds[0] == userId;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
                await transaction.DisposeAsync();
            }
        }
    }
}