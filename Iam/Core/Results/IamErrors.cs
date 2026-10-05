using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Iam.Core.Results;

public static class IamErrors
{
    public static class Users
    {
        public static readonly Error DisplayNameRequired = new("user.display_name_required", "A display name is required.");
        public static readonly Error DisplayNameTooLong = new("user.display_name_too_long", "The display name is too long (max 200 characters).");
        public static readonly Error ContactRequired = new("user.contact_required", "An email address or a phone number is required.");
        public static readonly Error InvalidCredentials = new("auth.invalid_credentials", "The login or password is incorrect.");
        public static readonly Error LockedOut = new("auth.locked_out", "The account is temporarily locked. Try again later.");
        public static readonly Error Disabled = new("auth.disabled", "The account is disabled.");
    }

    public static class Sessions
    {
        public static readonly Error Inactive = new("session.inactive", "The session has ended. Please sign in again.");
        public static readonly Error InvalidToken = new("session.invalid_token", "The refresh token is not valid.");
        public static readonly Error ReuseDetected = new("session.reuse_detected", "This refresh token was already used. The session has been closed for your safety.");
        public static readonly Error ConcurrentRefresh = new("session.concurrent_refresh", "Another refresh just happened. Retry with the latest token.");
    }

    public static class Roles
    {
        public static readonly Error NameRequired = new("role.name_required", "A role name is required.");
        public static readonly Error NameTooLong = new("role.name_too_long", "The role name is too long (max 100 characters).");
        public static readonly Error SystemRoleLocked = new("role.system_locked", "A built-in role cannot be renamed or deleted.");
    }

    public static class Permissions
    {
        public static readonly Error InvalidCode = new("permission.invalid_code", "A permission code looks like 'orders.view' or 'iam.users.view' (lower-case words separated by dots).");
    }

    public static class Clients
    {
        public static readonly Error InvalidClientId = new("client.invalid_client_id", "A client id uses 3 to 64 lower-case letters, digits or hyphens.");
        public static readonly Error InvalidName = new("client.invalid_name", "A client name is required (max 200 characters).");
        public static readonly Error InvalidPolicy = new("client.invalid_policy", "Access tokens last 1 to 60 minutes and sessions 1 to 365 days.");
    }
}