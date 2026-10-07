using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CoreKit.Tenant.Endpoints;

public static class TenantEndpointFilterExtensions
{
    /// <summary>For minimal APIs: app.MapGet(...).RequireTenant(). Answers 400 when no tenant was resolved.</summary>
    public static TBuilder RequireTenant<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
        => builder.AddEndpointFilter(async (context, next) =>
        {
            var tenant = context.HttpContext.RequestServices.GetRequiredService<ICurrentTenant>();

            if (tenant.IsResolved)
                return await next(context);

            return TenantErrors.TenantRequired.ToHttpResult();
        });
}

/// <summary>For controllers: [RequireTenant]. Answers 400 when no tenant was resolved.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireTenantAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var tenant = context.HttpContext.RequestServices.GetRequiredService<ICurrentTenant>();

        if (!tenant.IsResolved)
        {
            var error = TenantErrors.TenantRequired;

            context.Result = new ObjectResult(new { code = error.Code, message = error.Message })
            {
                StatusCode = TenantResultExtensions.StatusCodeFor(error.Kind)
            };

            return;
        }

        await next();
    }
}