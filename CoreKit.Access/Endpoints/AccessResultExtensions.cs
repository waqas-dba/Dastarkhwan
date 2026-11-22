using Microsoft.AspNetCore.Http;

namespace CoreKit.Access.Endpoints;

/// <summary>The one place that turns an AccessError into an HTTP status code using RFC 9457 Problem Details.</summary>
public static class AccessResultExtensions
{
    public static IResult ToHttpResult(this AccessResult result, Func<IResult>? onSuccess = null)
        => result.IsSuccess ? (onSuccess?.Invoke() ?? Results.NoContent()) : result.Error!.ToHttpResult();

    public static IResult ToHttpResult<T>(this AccessResult<T> result)
        => result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToHttpResult();

    public static IResult ToHttpResult(this PlatformMemberResult result)
        => result.IsSuccess ? Results.Ok(result) : result.Error!.ToHttpResult();

    public static IResult ToHttpResult(this AccessError error)
    {
        var (statusCode, title) = MapErrorKind(error.Kind);

        return Results.Problem(
            detail: error.Message,
            statusCode: statusCode,
            title: title,
            type: $"https://dastarkhwan.platform/errors/{error.Code}",
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = error.Code,
                ["traceId"] = System.Diagnostics.Activity.Current?.Id
            });
    }

    private static (int StatusCode, string Title) MapErrorKind(AccessErrorKind kind) => kind switch
    {
        AccessErrorKind.Validation => (StatusCodes.Status400BadRequest, "Invalid request"),
        AccessErrorKind.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized"),
        AccessErrorKind.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden"),
        AccessErrorKind.NotFound => (StatusCodes.Status404NotFound, "Not found"),
        AccessErrorKind.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
        _ => (StatusCodes.Status500InternalServerError, "Internal server error")
    };
}