namespace CoreKit.Access.Services;

/// <summary>Who is calling, where, and what they may do there.</summary>
internal sealed record AccessCall(AccessScope Scope, Guid UserId, EffectiveAccess Access);

/// <summary>
/// The check every role and assignment call starts with: there must be a scope and a signed-in user,
/// and the user must hold the permission in that scope. The scope always comes from the request context,
/// never from the caller's input, so one scope's roles can never be reached from another.
/// </summary>
internal sealed class AccessGuard
{
    private readonly IAccessScopeContext _scopeContext;
    private readonly IAccessActor _actor;
    private readonly IAccessService _access;

    public AccessGuard(IAccessScopeContext scopeContext, IAccessActor actor, IAccessService access)
    {
        _scopeContext = scopeContext;
        _actor = actor;
        _access = access;
    }

    public async Task<AccessResult<AccessCall>> RequireAsync(string permission, CancellationToken ct)
    {
        if (_scopeContext.Current is not { } scope || !scope.IsValid)
            return AccessErrors.ScopeRequired;

        if (_actor.UserId is not { } userId)
            return AccessErrors.NotAuthenticated;

        var access = await _access.GetAccessAsync(userId, scope, ct);

        if (!access.Has(permission))
            return AccessErrors.Denied;

        return AccessResult.Success(new AccessCall(scope, userId, access));
    }

    /// <summary>Nobody can hand out or change a permission they do not hold. Owners hold all of them.</summary>
    public static bool Holds(EffectiveAccess actor, IEnumerable<string> permissions)
        => actor.IsOwner || permissions.All(actor.Permissions.Contains);
}