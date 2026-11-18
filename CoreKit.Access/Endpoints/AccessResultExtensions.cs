namespace CoreKit.Access.Endpoints;

/// <summary>The one place that turns an AccessError into an HTTP status code.</summary>
public static class AccessResultExtensions
{
    public static IResult ToHttpResult(this AccessResult result, Func<IResult>? onSuccess = null)
        => result.IsSuccess ? (onSuccess?.Invoke() ?? Results.NoContent()) : result.Error!.ToHttpResult();

    public static IResult ToHttpResult<T>(this AccessResult<T> result)
        => result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToHttpResult();

    public static IResult ToHttpResult(this PlatformMemberResult result)
        => result.IsSuccess ? Results.Ok(result) : Results.Json(new { code = result.ErrorCode, message = result.Message }, statusCode: StatusCodeForErrorCode(result.ErrorCode));

    public static IResult ToHttpResult(this AccessError error)
        => Results.Json(new { code = error.Code, message = error.Message }, statusCode: StatusCodeFor(error.Kind));

    internal static int StatusCodeFor(AccessErrorKind kind) => kind switch
    {
        AccessErrorKind.Validation => StatusCodes.Status400BadRequest,
        AccessErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        AccessErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        AccessErrorKind.NotFound => StatusCodes.Status404NotFound,
        AccessErrorKind.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };

    internal static int StatusCodeForErrorCode(string? errorCode) => errorCode switch
    {
        "validation.failed" => StatusCodes.Status400BadRequest,
        "access.not_authenticated" => StatusCodes.Status401Unauthorized,
        "access.denied" => StatusCodes.Status403Forbidden,
        "access.escalation" => StatusCodes.Status403Forbidden,
        "access.scope_required" => StatusCodes.Status400BadRequest,
        "access.save_conflict" => StatusCodes.Status409Conflict,
        "access.concurrency_stamp_required" => StatusCodes.Status400BadRequest,
        "not_found" => StatusCodes.Status404NotFound,
        "conflict" => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };
}