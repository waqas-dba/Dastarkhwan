
namespace CoreKit.IAM.Authorization;


/// <summary>
/// Builds a policy on demand for any permission name, so [RequirePermission("anything.here")]
/// works without registering every permission up front. All other policies are left to ASP.NET Core.
/// </summary>
public sealed class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : base(options)
    {
    }

    public override async Task<Microsoft.AspNetCore.Authorization.AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (IamPolicies.TryGetPermission(policyName, out var permission))
        {
            return new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();
        }

        return await base.GetPolicyAsync(policyName);
    }
}
