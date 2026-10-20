namespace CoreKit.Access.Endpoints;

public static class AccessEndpointFilterExtensions
{
    /// <summary>
    /// Protects any route of the host application:
    /// app.MapPost("/sales", ...).RequireAuthorization().RequireAccess("sales.create").
    /// The person must hold the permission in the scope of the request (for example their tenant).
    /// Answers 400 without a scope, 401 when not signed in, 403 without the permission.
    /// </summary>
    public static TBuilder RequireAccess<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
        => builder.AddEndpointFilter(async (context, next) =>
        {
            var services = context.HttpContext.RequestServices;

            if (services.GetRequiredService<IAccessScopeContext>().Current is null)
                return AccessErrors.ScopeRequired.ToHttpResult();

            if (services.GetRequiredService<ICurrentUser>().UserId is null)
                return AccessErrors.NotAuthenticated.ToHttpResult();

            var allowed = await services.GetRequiredService<IAccessService>()
                .HasPermissionAsync(permission, context.HttpContext.RequestAborted);

            if (!allowed)
                return AccessErrors.Denied.ToHttpResult();

            return await next(context);
        });
}