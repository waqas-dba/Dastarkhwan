using CoreKit.IAM.Common;
using CoreKit.IAM.Interfaces;

namespace CoreKit.IAM.Services;

public sealed class AdministratorGuard : IAdministratorGuard
{
    private readonly IUserRepository _userRepository;

    public AdministratorGuard(IUserRepository userRepository) => _userRepository = userRepository;

    public async Task<bool> IsLastActiveAdministratorAsync(Guid userId, CancellationToken ct = default)
    {
        var activeAdministratorIds =
            await _userRepository.GetActiveUserIdsInRoleAsync(AdministratorRole.NormalizedName, ct);

        return activeAdministratorIds.Count == 1 && activeAdministratorIds[0] == userId;
    }
}