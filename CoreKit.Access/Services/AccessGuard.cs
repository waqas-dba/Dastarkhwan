using CoreKit.Access.Constants;
using CoreKit.Access.Contracts;
using CoreKit.Access.Common;

namespace CoreKit.Access.Services;

internal sealed record AccessCall(AccessScope Scope, Guid UserId, EffectiveAccess Access);

internal sealed class AccessGuard
{
    private readonly IAccessScopeContext _scopeContext;
    private readonly IAccessCurrentUser _actor;
    private readonly IAccessService _access;
    private readonly IAuthorityPolicy _authority;

    public AccessGuard(
        IAccessScopeContext scopeContext,
        IAccessCurrentUser actor,
        IAccessService access,
        IAuthorityPolicy authority)
    {
        _scopeContext = scopeContext;
        _actor = actor;
        _access = access;
        _authority = authority;
    }

    public Task<AccessResult<AccessCall>> RequireAsync(string permission, CancellationToken ct)
    {
        if (_scopeContext.Current is not { } scope || !scope.IsValid)
            return Task.FromResult<AccessResult<AccessCall>>(AccessErrors.ScopeRequired);
        return RequireAsync(scope, permission, ct);
    }

    public Task<AccessResult<AccessCall>> RequirePlatformScopePermissionAsync(
        string permission, CancellationToken ct)
        => RequireAsync(AccessScope.ForPlatform(AccessScope.PlatformId), permission, ct);

    private async Task<AccessResult<AccessCall>> RequireAsync(
        AccessScope scope, string permission, CancellationToken ct)
    {
        if (!_actor.IsAuthenticated || _actor.UserId is not { } userId || userId == Guid.Empty)
            return AccessErrors.NotAuthenticated;

        var access = _authority.RequiresFreshAuthority(permission)
            ? await _access.GetFreshAccessAsync(userId, scope, ct)
            : await _access.GetAccessAsync(userId, scope, ct);

        return access.Has(permission)
            ? AccessResult.Success(new AccessCall(scope, userId, access))
            : AccessErrors.Denied;
    }

    public static bool Holds(EffectiveAccess actor, IEnumerable<string> permissions)
        => actor.IsOwner || permissions.All(actor.Permissions.Contains);
}