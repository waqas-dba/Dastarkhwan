namespace CoreKit.Identity.Endpoints;

/// <summary>The one place that turns an IdentityError into an HTTP status code.</summary>
internal static class IdentityResultExtensions
{
    public static IResult ToHttpResult(this Common.IdentityResult result, Func<IResult>? onSuccess = null)
        => result.IsSuccess ? (onSuccess?.Invoke() ?? Results.NoContent()) : Problem(result.Error!);

    public static IResult ToHttpResult<T>(this IdentityResult<T> result)
        => result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error!);

    private static IResult Problem(Common.IdentityError error)
    {
        var status = error.Kind switch
        {
            IdentityErrorKind.Validation => StatusCodes.Status400BadRequest,
            IdentityErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
            IdentityErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            IdentityErrorKind.NotFound => StatusCodes.Status404NotFound,
            IdentityErrorKind.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        return Results.Json(new { code = error.Code, message = error.Message }, statusCode: status);
    }
}