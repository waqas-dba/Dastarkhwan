namespace CoreKit.Tenant.Extensions;

public static class TenantApplicationBuilderExtensions
{
    /// <summary>
    /// Finds the tenant of each request. Call it after app.UseIam(), because the tenant claim
    /// can only be read once the user is authenticated.
    /// </summary>
    public static IApplicationBuilder UseTenant(this IApplicationBuilder app)
        => app.UseMiddleware<TenantResolutionMiddleware>();
}