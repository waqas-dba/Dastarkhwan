using CoreKit.Identity.Contracts;
using CoreKit.Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CoreKit.Identity.Services;

public sealed class AdministratorGuard : IAdministratorGuard
{
    private readonly IPlatformAdministratorProvider _provider;
    private readonly IdentityDbContext _db;
    private const long AdminCheckLockKey = 0x4C41535441444D4E;

    public AdministratorGuard(IPlatformAdministratorProvider provider,
                              IUserRepository userRepository,
                              IdentityDbContext db)
    {
        _provider = provider;
        _db = db;
    }

    public async Task<bool> IsLastActiveAdministratorAsync(Guid userId, CancellationToken ct = default)
    {
        var ownsTransaction = _db.Database.CurrentTransaction is null;
        IDbContextTransaction? transaction = null;

        if (ownsTransaction)
            transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                $"SELECT pg_advisory_xact_lock({AdminCheckLockKey})", ct);

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