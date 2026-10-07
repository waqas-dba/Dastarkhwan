namespace CoreKit.Tenant.Endpoints;

/// <summary>The one place that turns a TenantError into an HTTP status code.</summary>
public static class TenantResultExtensions
{
    public static IResult ToHttpResult(this TenantResult result, Func<IResult>? onSuccess = null)
        => result.IsSuccess ? (onSuccess?.Invoke() ?? Results.NoContent()) : result.Error!.ToHttpResult();

    public static IResult ToHttpResult<T>(this TenantResult<T> result)
        => result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToHttpResult();

    public static IResult ToHttpResult(this TenantError error)
        => Results.Json(new { code = error.Code, message = error.Message }, statusCode: StatusCodeFor(error.Kind));

    internal static int StatusCodeFor(TenantErrorKind kind) => kind switch
    {
        TenantErrorKind.Validation => StatusCodes.Status400BadRequest,
        TenantErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        TenantErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        TenantErrorKind.NotFound => StatusCodes.Status404NotFound,
        TenantErrorKind.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };
}