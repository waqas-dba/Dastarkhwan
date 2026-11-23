using Microsoft.AspNetCore.Http;

namespace CoreKit.Tenant.Endpoints;

/// <summary>The one place that turns a TenantError into an HTTP status code.</summary>
public static class TenantResultExtensions
{
    public static IResult ToHttpResult(this TenantResult result, Func<IResult>? onSuccess = null)
        => result.IsSuccess ? (onSuccess?.Invoke() ?? Results.NoContent()) : result.Error!.ToHttpResult();

    public static IResult ToHttpResult<T>(this TenantResult<T> result)
        => result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToHttpResult();

    public static IResult ToHttpResult(this TenantError error)
    {
        var (statusCode, title) = MapErrorKind(error.Kind);

        return Results.Problem(
            detail: error.Message,
            statusCode: statusCode,
            title: title,
            type: $"https://dastarkhwan.platform/errors/{error.Code}",
            instance: "/",
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = error.Code,
                ["traceId"] = System.Diagnostics.Activity.Current?.Id
            });
    }

    private static (int StatusCode, string Title) MapErrorKind(TenantErrorKind kind) => kind switch
    {
        TenantErrorKind.Validation => (StatusCodes.Status400BadRequest, "Invalid request"),
        TenantErrorKind.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized"),
        TenantErrorKind.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden"),
        TenantErrorKind.NotFound => (StatusCodes.Status404NotFound, "Not found"),
        TenantErrorKind.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
        _ => (StatusCodes.Status500InternalServerError, "Internal server error")
    };

    public static int StatusCodeFor(TenantErrorKind kind) => kind switch
    {
        TenantErrorKind.Validation => StatusCodes.Status400BadRequest,
        TenantErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        TenantErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        TenantErrorKind.NotFound => StatusCodes.Status404NotFound,
        TenantErrorKind.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };
}