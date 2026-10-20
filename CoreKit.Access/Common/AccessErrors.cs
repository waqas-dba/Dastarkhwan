namespace CoreKit.Access.Common;

public static class AccessErrors
{
    public static readonly AccessError ScopeRequired = new(
        AccessErrorKind.Validation, "access.scope_required",
        "This request needs a scope (for example a tenant), but none was identified.");

    public static readonly AccessError NotAuthenticated = new(
        AccessErrorKind.Unauthorized, "access.not_authenticated", "You are not signed in.");

    public static readonly AccessError Denied = new(
        AccessErrorKind.Forbidden, "access.denied", "You do not have permission to do this.");

    public static readonly AccessError Escalation = new(
        AccessErrorKind.Forbidden, "access.escalation",
        "You can only give or change permissions that you hold yourself.");

    public static readonly AccessError SaveConflict = new(
        AccessErrorKind.Conflict, "access.save_conflict",
        "The change conflicts with another change. Please reload and try again.");

    public static AccessError Validation(string message)
        => new(AccessErrorKind.Validation, "validation.failed", message);

    public static AccessError NotFound(string what)
        => new(AccessErrorKind.NotFound, "not_found", $"{what} was not found.");

    public static AccessError Conflict(string message)
        => new(AccessErrorKind.Conflict, "conflict", message);
}