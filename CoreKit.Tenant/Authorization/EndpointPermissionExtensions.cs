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
}
