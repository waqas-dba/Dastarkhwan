using CoreKit.Access.Contracts;
using CoreKit.Identity.Contracts;

namespace Dastarkhwan.Integration.Membership;

public sealed class AccessPlatformAdministratorProvider : IPlatformAdministratorProvider
{
    private readonly IAccessService _access;
    public AccessPlatformAdministratorProvider(IAccessService access) => _access = access;

    public Task<IReadOnlyList<Guid>> GetPlatformAdministratorUserIdsAsync(CancellationToken ct = default)
    {
        var scope = AccessScope.ForPlatform(AccessScope.PlatformId);
        return _access.ListUserIdsWithRoleAsync(scope, "Administrator", null, ct);
    }
}