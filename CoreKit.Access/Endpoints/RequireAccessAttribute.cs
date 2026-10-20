using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CoreKit.Access.Endpoints;

/// <summary>
/// For controllers: [RequireAccess("sales.create")] on a class or action. The person must hold the
/// permission in the scope of the request. Answers 400 without a scope, 401 when not signed in,
/// 403 without the permission.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireAccessAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _permission;

    public RequireAccessAttribute(string permission) => _permission = permission;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        AccessError? error = null;

        if (services.GetRequiredService<IAccessScopeContext>().Current is null)
            error = AccessErrors.ScopeRequired;
        else if (services.GetRequiredService<ICurrentUser>().UserId is null)
            error = AccessErrors.NotAuthenticated;
        else if (!await services.GetRequiredService<IAccessService>()
                     .HasPermissionAsync(_permission, context.HttpContext.RequestAborted))
            error = AccessErrors.Denied;

        if (error is not null)
        {
            context.Result = new ObjectResult(new { code = error.Code, message = error.Message })
            {
                StatusCode = AccessResultExtensions.StatusCodeFor(error.Kind)
            };

            return;
        }

        await next();
    }
}