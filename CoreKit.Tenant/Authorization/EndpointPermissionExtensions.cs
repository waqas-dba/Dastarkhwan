using CoreKit.Access.Abstractions;
using CoreKit.Access.Common;
using CoreKit.Access.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CoreKit.Tenant.Authorization;

/// <summary>
/// Applies the shared permission-policy name to a Tenant endpoint without taking a dependency on the
/// Identity module. The host registers the policy provider that understands these names.
/// </summary>
public static class EndpointPermissionExtensions
{
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(PermissionPolicy.NameFor(permission));

    /// <summary>
    /// Requires that the signed-in user holds the named Access permission in the current tenant scope.
    /// Resolves the tenant scope from <see cref="IAccessScopeContext"/> (populated from
    /// <see cref="ICurrentTenant"/>) and the user identity from <see cref="IAccessCurrentUser"/>.
    /// Fails closed when no tenant scope is resolved, the user is not authenticated, or Access
    /// authorization denies the permission.
    /// </summary>
    public static TBuilder RequireAccessPermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
        => builder.AddEndpointFilter(async (context, next) =>
        {
            var scopeContext = context.HttpContext.RequestServices.GetRequiredService<IAccessScopeContext>();
            var currentUser = context.HttpContext.RequestServices.GetRequiredService<IAccessCurrentUser>();
            var permissionChecker = context.HttpContext.RequestServices.GetRequiredService<CoreKit.Access.Abstractions.IPermissionChecker>();

            // Fail closed: no tenant scope resolved.
            if (scopeContext.Current is not { } scope || !scope.IsValid)
                return AccessErrors.ScopeRequired.ToHttpResult();

            // Fail closed: not authenticated. A non-empty UserId alone is not proof of
            // authentication; IsAuthenticated must also be true.
            if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
                return AccessErrors.NotAuthenticated.ToHttpResult();

            // Check permission through Access (fresh authority for management operations).
            if (!await permissionChecker.HasPermissionAsync(permission, context.HttpContext.RequestAborted))
                return AccessErrors.Denied.ToHttpResult();

            return await next(context);
        });
}
