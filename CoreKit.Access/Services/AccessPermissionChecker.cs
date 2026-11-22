namespace CoreKit.Access.Services;

/// <summary>Lets any module ask "may the signed-in user do this here?" through Access.</summary>
internal sealed class AccessPermissionChecker : IPermissionChecker
{
    private readonly IAccessService _access;
    private readonly IAccessCurrentUser _actor;
    private readonly IAccessScopeContext _scopeContext;

    public AccessPermissionChecker(IAccessService access, IAccessCurrentUser actor, IAccessScopeContext scopeContext)
    {
        _access = access;
        _actor = actor;
        _scopeContext = scopeContext;
    }

    public Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default)
        => _access.HasPermissionAsync(permission, ct);

    public async Task<bool> HasFreshPermissionAsync(string permission, CancellationToken ct = default)
    {
        // Fail closed: no scope, no user, or not authenticated.
        if (_scopeContext.Current is not { } scope || !scope.IsValid)
            return false;
        if (_actor.UserId is not { } userId || userId == Guid.Empty || !_actor.IsAuthenticated)
            return false;

        var freshAccess = await _access.GetFreshAccessAsync(userId, scope, ct);
        return freshAccess.Has(permission);
    }

    public async Task<bool> HasPlatformPermissionAsync(string permission, CancellationToken ct = default)
    {
        // Fail closed: no user, or not authenticated, grants nothing.
        if (_actor.UserId is not { } userId || !_actor.IsAuthenticated)
            return false;

        return await _access.HasPermissionAsync(userId, AccessScope.ForPlatform(AccessScope.PlatformId), permission, ct);
    }
}
