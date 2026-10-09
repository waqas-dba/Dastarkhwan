namespace CoreKit.IAM.Endpoints;

/// <summary>The one place that turns an IamError into an HTTP status code.</summary>
internal static class IamResultExtensions
{
    public static IResult ToHttpResult(this IamResult result, Func<IResult>? onSuccess = null)
        => result.IsSuccess ? (onSuccess?.Invoke() ?? Results.NoContent()) : Problem(result.Error!);

    public static IResult ToHttpResult<T>(this IamResult<T> result)
        => result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error!);

    private static IResult Problem(IamError error)
    {
        var status = error.Kind switch
        {
            IamErrorKind.Validation => StatusCodes.Status400BadRequest,
            IamErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
            IamErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            IamErrorKind.NotFound => StatusCodes.Status404NotFound,
            IamErrorKind.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        return Results.Json(new { code = error.Code, message = error.Message }, statusCode: status);
    }
}