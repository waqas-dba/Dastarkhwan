using CoreKit.Identity.Interfaces;
using CoreKit.Identity.Repositories;

namespace CoreKit.Identity.Services;

/// <summary>
/// Prevents the one mistake that locks everybody out: removing, deactivating or deleting the last
/// administrator. Delegates to an <see cref="IPlatformAdministratorProvider"/> (implemented by Access)
/// for the authoritative admin list, and verifies the user is active in Identity.
/// </summary>
public sealed class AdministratorGuard : IAdministratorGuard
{
    private readonly IPlatformAdministratorProvider _provider;
    private readonly IUserRepository _userRepository;
    private readonly SemaphoreSlim _lastAdminLock = new(1, 1);

    public AdministratorGuard(IPlatformAdministratorProvider provider, IUserRepository userRepository)
    {
        _provider = provider;
        _userRepository = userRepository;
    }

    public async Task<bool> IsLastActiveAdministratorAsync(Guid userId, CancellationToken ct = default)
    {
        // Serialize the check to prevent concurrent deactivation of the last two administrators.
        // Note: This is an in-process lock. For multi-instance deployments, a distributed lock
        // (e.g., PostgreSQL advisory lock or Redis lock) would be required.
        await _lastAdminLock.WaitAsync(ct);
        try
        {
            var adminUserIds = await _provider.GetPlatformAdministratorUserIdsAsync(ct);

            // Filter out inactive Identity accounts. Access role holders may include users
            // that have been deactivated in Identity; they should not count as active admins.
            var activeAdminIds = new List<Guid>();
            foreach (var adminId in adminUserIds)
            {
                var user = await _userRepository.FindByIdAsync(adminId, ct);
                if (user is not null && user.IsActive)
                {
                    activeAdminIds.Add(adminId);
                }
            }

            if (activeAdminIds.Count != 1)
                return false;

            var soleAdminId = activeAdminIds[0];
            return soleAdminId == userId;
        }
        finally
        {
            _lastAdminLock.Release();
        }
    }
}
