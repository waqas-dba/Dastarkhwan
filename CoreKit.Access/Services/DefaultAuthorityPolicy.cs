using CoreKit.Access.Constants;
using CoreKit.Access.Contracts;

namespace CoreKit.Access.Services;

internal sealed class DefaultAuthorityPolicy : IAuthorityPolicy
{
    private readonly HashSet<string> _fresh;

    public DefaultAuthorityPolicy(IEnumerable<IAuthorityPolicyContributor> contributors)
        => _fresh = contributors
            .SelectMany(c => c.GetFreshAuthorityPermissions())
            .ToHashSet(StringComparer.Ordinal);

    public bool RequiresFreshAuthority(string permission) => _fresh.Contains(permission);
}

internal sealed class BuiltInAuthorityPolicyContributor : IAuthorityPolicyContributor
{
    public IEnumerable<string> GetFreshAuthorityPermissions()
    {
        yield return AccessPermissionNames.RolesManage;
        yield return AccessPermissionNames.AssignmentsManage;
        yield return AccessPermissionNames.PlatformMembersManage;
    }
}