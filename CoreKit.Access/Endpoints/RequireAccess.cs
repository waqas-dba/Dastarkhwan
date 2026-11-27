using CoreKit.Access.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CoreKit.Access.Endpoints;

public static class AccessEndpointFilterExtensions
{
    /// <summary>
    /// Protects any route of the host application:
    /// app.MapPost("/sales", ...).RequireAuthorization().RequireAccess("sales.create").
    /// The person must hold the permission in the scope of the request (for example their tenant).
    /// Answers 400 without a scope, 401 when not signed in, 403 without the permission.
    ///
    /// The filter is built per request through ActivatorUtilities against the request's own provider, the
    /// same mechanism AddEndpointFilter&lt;TFilter&gt; uses. That keeps the dependency graph constructor-injected
    /// and scoped to the request; reading services out of HttpContext.RequestServices inside the filter would
    /// hide the dependency and make the filter impossible to test or reason about.
    /// </summary>
    public static TBuilder RequireAccess<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
        => builder.AddEndpointFilterFactory((factoryContext, next) => context =>
        {
            var filter = ActivatorUtilities.CreateInstance<RequireAccessFilter>(
                context.HttpContext.RequestServices, permission);

            return filter.InvokeAsync(context, next);
        });
}

/// <summary>
/// The authorization filter behind <see cref="AccessEndpointFilterExtensions.RequireAccess{TBuilder}"/>.
/// One instance per request, with every dependency supplied by the constructor from the request scope.
/// </summary>
public sealed class RequireAccessFilter : IEndpointFilter
{
    private readonly string _permission;
    private readonly IAccessScopeContext _scopeContext;
    private readonly IAccessCurrentUser _currentUser;
    private readonly IAccessService _access;

    public RequireAccessFilter(
        string permission,
        IAccessScopeContext scopeContext,
        IAccessCurrentUser currentUser,
        IAccessService access)
    {
        _permission = permission;
        _scopeContext = scopeContext;
        _currentUser = currentUser;
        _access = access;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        // Fail closed, in order: a missing scope means the request never reached a tenant or store,
        // and an unidentified caller cannot hold anything.
        if (_scopeContext.Current is null)
            return AccessErrors.ScopeRequired.ToHttpResult();

        if (_currentUser.UserId is not { } userId || userId == Guid.Empty || !_currentUser.IsAuthenticated)
            return AccessErrors.NotAuthenticated.ToHttpResult();

        // Write permissions (role/assignment/platform membership management) use fresh access
        // that bypasses the cache, so a permission just revoked is not still honored through a
        // remembered copy. Read permissions use the cached path for performance.
        bool allowed;
        if (AccessGuard.RequiresFreshAuthority(_permission))
        {
            if (_access is not IFreshAccessSource fresh)
                return AccessErrors.Denied.ToHttpResult();
            var freshAccess = await fresh.GetFreshAccessAsync(
                userId, _scopeContext.Current!.Value, context.HttpContext.RequestAborted);
            allowed = freshAccess.Has(_permission);
        }
        else
        {
            allowed = await _access.HasPermissionAsync(_permission, context.HttpContext.RequestAborted);
        }

        if (!allowed)
            return AccessErrors.Denied.ToHttpResult();

        return await next(context);
    }

    /// <summary>
    /// Write permissions require fresh authority: changing roles or assignments is decided on the
    /// person's permissions right now, not from a remembered copy.
    /// </summary>
    private static bool RequiresFreshAuthority(string permission)
        => AccessGuard.RequiresFreshAuthority(permission);
}