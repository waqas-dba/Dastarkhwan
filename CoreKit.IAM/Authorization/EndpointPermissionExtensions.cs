
namespace CoreKit.IAM.Authorization;

public static class EndpointPermissionExtensions
{
    /// <summary>For minimal APIs: app.MapGet(...).RequirePermission("users.read").</summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(IamPolicies.ForPermission(permission));
}
