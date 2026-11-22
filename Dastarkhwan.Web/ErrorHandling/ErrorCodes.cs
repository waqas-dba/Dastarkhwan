namespace Dastarkhwan.Web.ErrorHandling;

/// <summary>Stable, machine-readable error codes for the platform Problem Details contract.</summary>
public static class ErrorCodes
{
    // Client errors (4xx)
    public const string ValidationFailed = "validation_failed";
    public const string InvalidRequest = "invalid_request";
    public const string NotFound = "not_found";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string Conflict = "conflict";
    public const string IdempotencyConflict = "idempotency_conflict";
    public const string AccountLocked = "account_locked";
    public const string EmailNotConfirmed = "email_not_confirmed";
    public const string RateLimited = "rate_limited";
    public const string TenantContextMissing = "tenant_context_missing";
    public const string TenantAccessDenied = "tenant_access_denied";

    // Server errors (5xx)
    public const string InternalError = "internal_error";
    public const string DatabaseUnavailable = "database_unavailable";
    public const string ConcurrencyFailure = "concurrency_failure";
    public const string ExternalDependencyFailure = "external_dependency_failure";
    public const string RequestCancelled = "request_cancelled";
}
