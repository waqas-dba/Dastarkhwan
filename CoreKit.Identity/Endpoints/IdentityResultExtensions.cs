namespace CoreKit.Identity.Endpoints;

/// <summary>The one place that turns an IdentityError into an HTTP status code using RFC 9457 Problem Details.</summary>
internal static class IdentityResultExtensions
{
    public static IResult ToHttpResult(this Common.IdentityResult result, Func<IResult>? onSuccess = null)
        => result.IsSuccess ? (onSuccess?.Invoke() ?? Results.NoContent()) : Problem(result.Error!);

    public static IResult ToHttpResult<T>(this IdentityResult<T> result)
        => result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error!);

    private static IResult Problem(Common.IdentityError error)
    {
        var (statusCode, title) = error.Kind switch
        {
            IdentityErrorKind.Validation => (StatusCodes.Status400BadRequest, "Invalid request"),
            IdentityErrorKind.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            IdentityErrorKind.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden"),
            IdentityErrorKind.NotFound => (StatusCodes.Status404NotFound, "Not found"),
            IdentityErrorKind.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status500InternalServerError, "Internal server error")
        };

        return Results.Json(new
        {
            type = $"https://dastarkhwan.platform/errors/{error.Code}",
            title = title,
            status = statusCode,
            detail = error.Message,
            errorCode = error.Code,
            instance = string.Empty
        }, statusCode: statusCode, contentType: "application/problem+json");
    }
}