using CoreKit.Access.Endpoints;

namespace CoreKit.Tenant.Authorization;

/// <summary>
/// Protects a Tenant endpoint with an Access permission, evaluated in the current tenant scope.
/// This is a thin wrapper over Access's <see cref="AccessEndpointFilterExtensions.RequireAccess"/> so the
/// platform has exactly one endpoint-authorization filter implementation.
/// </summary>
public static class EndpointPermissionExtensions
{
    public static TBuilder RequireAccessPermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
        => builder.RequireAccess(permission);
}