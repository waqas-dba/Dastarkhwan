using CoreKit.Access.Abstractions;
using CoreKit.Identity.Constants;
using CoreKit.Identity.Interfaces;

namespace CoreKit.Identity.Services;

/// <summary>
/// Prevents the one mistake that locks everybody out: removing, deactivating or deleting the last
/// administrator. Administrators are current platform members holding the Administrator role in the
/// platform scope (Access is the source of truth); deactivating a user revokes their platform
/// membership, so a deactivated administrator is never counted.
/// </summary>
public sealed class AdministratorGuard : IAdministratorGuard
{
    private static readonly AccessScope PlatformScope = AccessScope.ForPlatform(AccessScope.PlatformId);

    private readonly IAccessService _access;

    public AdministratorGuard(IAccessService access) => _access = access;

    public async Task<bool> IsLastActiveAdministratorAsync(Guid userId, CancellationToken ct = default)
    {
        var adminIds = await _access.ListUserIdsWithRoleAsync(PlatformScope, IdentityRoleNames.Administrator, ct);

        return adminIds.Count == 1 && adminIds[0] == userId;
    }
}