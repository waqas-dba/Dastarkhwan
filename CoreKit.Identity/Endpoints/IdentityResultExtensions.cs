using CoreKit.Abstractions.Errors;
using Microsoft.AspNetCore.Http;

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
        var (statusCode, title) = ProblemDetailsFactory.Map(error.Kind switch
        {
            IdentityErrorKind.Validation => ErrorKind.Validation,
            IdentityErrorKind.Unauthorized => ErrorKind.Unauthorized,
            IdentityErrorKind.Forbidden => ErrorKind.Forbidden,
            IdentityErrorKind.NotFound => ErrorKind.NotFound,
            IdentityErrorKind.Conflict => ErrorKind.Conflict,
            _ => ErrorKind.Internal
        });

        return ProblemDetailsFactory.Create(
            errorCode: error.Code,
            title: title,
            detail: error.Message,
            statusCode: statusCode,
            traceId: System.Diagnostics.Activity.Current?.Id);
    }
}