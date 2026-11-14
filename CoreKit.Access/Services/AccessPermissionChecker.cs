namespace CoreKit.Access.Services;

/// <summary>Lets any module ask "may the signed-in user do this here?" through Access.</summary>
internal sealed class AccessPermissionChecker : IPermissionChecker
{
    private readonly IAccessService _access;
    private readonly IAccessCurrentUser _actor;

    public AccessPermissionChecker(IAccessService access, IAccessCurrentUser actor)
    {
        _access = access;
        _actor = actor;
    }

    public Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default)
        => _access.HasPermissionAsync(permission, ct);

    public async Task<bool> HasPlatformPermissionAsync(string permission, CancellationToken ct = default)
    {
        // Fail closed: no user, or not authenticated, grants nothing.
        if (_actor.UserId is not { } userId || !_actor.IsAuthenticated)
            return false;

        return await _access.HasPermissionAsync(userId, AccessScope.ForPlatform(AccessScope.PlatformId), permission, ct);
    }
}
