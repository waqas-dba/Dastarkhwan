namespace CoreKit.Tenant.Common;

public static class TenantErrors
{
    public static readonly TenantError TenantRequired = new(
        TenantErrorKind.Validation, "tenant.required", "This request needs a tenant, but none was identified.");

    public static readonly TenantError UnknownTenant = new(
        TenantErrorKind.NotFound, "tenant.not_found", "The tenant was not found.");

    public static readonly TenantError Mismatch = new(
        TenantErrorKind.Forbidden, "tenant.mismatch", "The request identifies more than one tenant.");

    public static readonly TenantError CrossTenantAccess = new(
        TenantErrorKind.Forbidden, "tenant.cross_tenant_access", "You cannot access another tenant.");

    public static readonly TenantError PlatformOnly = new(
        TenantErrorKind.Forbidden, "tenant.platform_only", "Only platform administrators can do this.");

    public static readonly TenantError NotAuthenticated = new(
        TenantErrorKind.Unauthorized, "tenant.not_authenticated", "You are not signed in.");

    public static readonly TenantError ArchivedReadOnly = new(
        TenantErrorKind.Conflict, "tenant.read_only", "An archived tenant is read-only. Restore it first.");

    public static readonly TenantError SaveConflict = new(
        TenantErrorKind.Conflict, "tenant.save_conflict",
        "The change conflicts with another change. Please reload and try again.");

    /// <summary>The error that explains why a tenant in this status cannot serve requests.</summary>
    public static TenantError ForStatus(TenantStatus status) => status switch
    {
        TenantStatus.Pending => new(TenantErrorKind.Forbidden, "tenant.pending", "This tenant has not been activated yet."),
        TenantStatus.Suspended => new(TenantErrorKind.Forbidden, "tenant.suspended", "This tenant is suspended."),
        TenantStatus.Archived => new(TenantErrorKind.Forbidden, "tenant.archived", "This tenant has been archived."),
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "An active tenant has no blocking error.")
    };

    public static TenantError InvalidTransition(TenantStatus from, TenantStatus to)
        => new(TenantErrorKind.Conflict, "tenant.invalid_transition", $"A {from} tenant cannot become {to}.");

    public static TenantError Validation(string message)
        => new(TenantErrorKind.Validation, "validation.failed", message);

    public static TenantError NotFound(string what)
        => new(TenantErrorKind.NotFound, "not_found", $"{what} was not found.");

    public static TenantError Conflict(string message)
        => new(TenantErrorKind.Conflict, "conflict", message);

    public static TenantError Forbidden(string message)
        => new(TenantErrorKind.Forbidden, "forbidden", message);
}