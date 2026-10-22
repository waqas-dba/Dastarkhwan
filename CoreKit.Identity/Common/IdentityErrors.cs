
namespace CoreKit.Identity.Common;

public static class IdentityErrors   
{
    public static readonly IdentityError InvalidCredentials = new(
        IdentityErrorKind.Unauthorized, "auth.invalid_credentials", "The email or password is incorrect.");

    public static readonly IdentityError InvalidRefreshToken = new(
        IdentityErrorKind.Unauthorized, "auth.invalid_refresh_token", "The refresh token is not valid. Please sign in again.");

    public static readonly IdentityError NotAuthenticated = new(
        IdentityErrorKind.Unauthorized, "auth.not_authenticated", "You are not signed in.");

    // 400, not 401: a clients treats 401 as "your session ended", which is not what happened here.
    public static readonly IdentityError WrongCurrentPassword = new(
        IdentityErrorKind.Validation, "auth.wrong_current_password", "The current password is incorrect.");

    public static IdentityError Validation(string message)
        => new(IdentityErrorKind.Validation, "validation.failed", message);

    public static IdentityError NotFound(string what)
        => new(IdentityErrorKind.NotFound, "not_found", $"{what} was not found.");

    public static IdentityError Conflict(string message)
        => new(IdentityErrorKind.Conflict, "conflict", message);

    public static IdentityError Forbidden(string message)
        => new(IdentityErrorKind.Forbidden, "forbidden", message);
}
