using CoreKit.Abstractions.Errors;
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
        var (statusCode, title) = ProblemDetailsFactory.Map(error.Kind switch
        {
            AccessErrorKind.Validation => ErrorKind.Validation,
            AccessErrorKind.Unauthorized => ErrorKind.Unauthorized,
            AccessErrorKind.Forbidden => ErrorKind.Forbidden,
            AccessErrorKind.NotFound => ErrorKind.NotFound,
            AccessErrorKind.Conflict => ErrorKind.Conflict,
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