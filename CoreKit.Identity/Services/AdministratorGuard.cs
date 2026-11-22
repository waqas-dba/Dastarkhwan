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

    public AdministratorGuard(IPlatformAdministratorProvider provider, IUserRepository userRepository)
    {
        _provider = provider;
        _userRepository = userRepository;
    }

    public async Task<bool> IsLastActiveAdministratorAsync(Guid userId, CancellationToken ct = default)
    {
        var adminUserIds = await _provider.GetPlatformAdministratorUserIdsAsync(ct);

        if (adminUserIds.Count != 1)
            return false;

        var soleAdminId = adminUserIds[0];
        if (soleAdminId != userId)
            return false;

        var user = await _userRepository.FindByIdAsync(soleAdminId, ct);
        return user is not null && user.IsActive;
    }
}
