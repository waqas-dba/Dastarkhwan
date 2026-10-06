
using Microsoft.AspNetCore.Builder;

namespace CoreKit.IAM.Extensions;



public static class IamApplicationBuilderExtensions
{
    /// <summary>
    /// Adds IAM's middleware in the right order: who are you, are you being too fast, may you do this.
    /// A wrong order is a classic security bug, so the module does it for the host.
    /// </summary>
    public static IApplicationBuilder UseIam(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();

        return app;
    }
}
