
namespace CoreKit.IAM.Common;

public static class IamErrors   
{
    public static readonly IamError InvalidCredentials = new(
        IamErrorKind.Unauthorized, "auth.invalid_credentials", "The email or password is incorrect.");

    public static readonly IamError InvalidRefreshToken = new(
        IamErrorKind.Unauthorized, "auth.invalid_refresh_token", "The refresh token is not valid. Please sign in again.");

    public static readonly IamError NotAuthenticated = new(
        IamErrorKind.Unauthorized, "auth.not_authenticated", "You are not signed in.");

    // 400, not 401: a clients treats 401 as "your session ended", which is not what happened here.
    public static readonly IamError WrongCurrentPassword = new(
        IamErrorKind.Validation, "auth.wrong_current_password", "The current password is incorrect.");

    public static IamError Validation(string message)
        => new(IamErrorKind.Validation, "validation.failed", message);

    public static IamError NotFound(string what)
        => new(IamErrorKind.NotFound, "not_found", $"{what} was not found.");

    public static IamError Conflict(string message)
        => new(IamErrorKind.Conflict, "conflict", message);

    public static IamError Forbidden(string message)
        => new(IamErrorKind.Forbidden, "forbidden", message);
}
