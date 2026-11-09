namespace CoreKit.Access.Services;

/// <summary>
/// Permissions read from the database right now, never from the cache. Decisions that change who may do what
/// (managing roles and assignments) use this, so a person whose access was just withdrawn cannot keep using
/// a remembered copy of it.
/// </summary>
internal interface IFreshAccessSource
{
    Task<EffectiveAccess> GetFreshAccessAsync(Guid userId, AccessScope scope, CancellationToken ct = default);
}

internal sealed class AccessService : IAccessService, IFreshAccessSource
{
    private readonly IAccessMembership _membership;
    private readonly IAccessAssignmentRepository _assignments;
    private readonly IAccessCache _cache;
    private readonly AccessCatalog _catalog;
    private readonly IAccessScopeContext _scopeContext;
    private readonly ICurrentUser _actor;

    public AccessService(
        IAccessMembership membership,
        IAccessAssignmentRepository assignments,
        IAccessCache cache,
        AccessCatalog catalog,
        IAccessScopeContext scopeContext,
        ICurrentUser actor)
    {
        _membership = membership;
        _assignments = assignments;
        _cache = cache;
        _catalog = catalog;
        _scopeContext = scopeContext;
        _actor = actor;
    }

    public async Task<EffectiveAccess> GetAccessAsync(Guid userId, AccessScope scope, CancellationToken ct = default)
    {
        if (userId == Guid.Empty || !scope.IsValid)
            return EffectiveAccess.None;

        if (_cache.TryGet(scope, userId, out var cached))
            return cached;

        // Note which cache generation this lookup starts in. If a role or assignment changes while the database
        // is being read, the result is stale and the cache refuses to store it.
        var token = _cache.Begin(scope, userId);

        var access = await ResolveAsync(userId, scope, ct);

        // Not a member: nothing is granted, and nothing is remembered (they may be added a moment later).
        if (access.IsMember)
            _cache.Set(scope, userId, access, token);

        return access;
    }

    public async Task<EffectiveAccess> GetFreshAccessAsync(Guid userId, AccessScope scope, CancellationToken ct = default)
    {
        if (userId == Guid.Empty || !scope.IsValid)
            return EffectiveAccess.None;

        return await ResolveAsync(userId, scope, ct);
    }

    private async Task<EffectiveAccess> ResolveAsync(Guid userId, AccessScope scope, CancellationToken ct)
    {
        var member = await _membership.FindAsync(scope, userId, ct);

        if (member is null)
            return EffectiveAccess.None;

        var roles = await _assignments.ListRolesForUserAsync(scope, userId, ct);

        var permissions = roles
            .SelectMany(r => r.Permissions.Select(p => p.Permission))
            .ToHashSet(StringComparer.Ordinal);

        if (member.IsOwner)
            permissions.UnionWith(_catalog.Names);

        return new EffectiveAccess(
            true, member.IsOwner, permissions, roles.Select(r => r.Name).Order(StringComparer.Ordinal).ToList());
    }

    public async Task<bool> HasPermissionAsync(
        Guid userId, AccessScope scope, string permission, CancellationToken ct = default)
        => (await GetAccessAsync(userId, scope, ct)).Has(permission);

    public async Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default)
    {
        if (_scopeContext.Current is not { } scope || _actor.UserId is not { } userId)
            return false;

        return await HasPermissionAsync(userId, scope, permission, ct);
    }

    public async Task<AccessResult<EffectiveAccess>> GetMyAccessAsync(CancellationToken ct = default)
    {
        if (_scopeContext.Current is not { } scope || !scope.IsValid)
            return AccessErrors.ScopeRequired;

        if (_actor.UserId is not { } userId)
            return AccessErrors.NotAuthenticated;

        return AccessResult.Success(await GetAccessAsync(userId, scope, ct));
    }
}
