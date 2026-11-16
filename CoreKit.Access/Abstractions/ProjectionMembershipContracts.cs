namespace CoreKit.Access.Abstractions;

/// <summary>
/// The result of an authoritative projection membership lookup. The projection is the
/// authoritative source for tenant membership, so a lookup never falls back to another
/// source. Every failure state is recorded explicitly: a missing row, a stale watermark,
/// a quarantined tenant, an inactive tenant, an unknown tenant state, or an exception
/// while reading the projection.
/// </summary>
public sealed class ProjectionAccessMembershipResult
{
    private ProjectionAccessMembershipResult(
        AccessMember? member, ProjectionAccessMembershipError? error, Exception? exception, bool isAuthoritative)
    {
        Member = member;
        Error = error;
        Exception = exception;
        IsAuthoritative = isAuthoritative;
    }

    /// <summary>The member when the projection is trusted and the person belongs to the scope.</summary>
    public AccessMember? Member { get; }

    /// <summary>The failure kind when the projection could not be trusted. Null when the lookup succeeded.</summary>
    public ProjectionAccessMembershipError? Error { get; }

    /// <summary>
    /// The exception when <see cref="Error"/> is <see cref="ProjectionAccessMembershipError.ProjectionFailure"/>.
    /// Internal: callers must never branch on raw projection exceptions, only on <see cref="Error"/>.
    /// </summary>
    internal Exception? Exception { get; }

    /// <summary>True when this result is authoritative for the requested scope, so the caller must
    /// not fall through to another membership source.</summary>
    public bool IsAuthoritative { get; }

    /// <summary>True when the projection is trusted and a member record was found.</summary>
    public bool IsFound => Member is not null && Error is null && IsAuthoritative;

    /// <summary>True when the projection is authoritative but the person is not a member. The caller
    /// must deny; it must not fall through to another source.</summary>
    public bool IsNotFound => Member is null && Error is null && IsAuthoritative;

    /// <summary>True when the projection is authoritative but could not be trusted. The caller must deny.</summary>
    public bool IsError => Error is not null && IsAuthoritative;

    /// <summary>True when this adapter is not authoritative for the requested scope, so the caller
    /// may continue to the next membership source.</summary>
    public bool IsNonAuthoritative => !IsAuthoritative;

    public static ProjectionAccessMembershipResult Found(AccessMember member) =>
        new(member, null, null, true);

    public static ProjectionAccessMembershipResult NotFound() =>
        new(null, null, null, true);

    public static ProjectionAccessMembershipResult Failure(ProjectionAccessMembershipError error, Exception? exception = null) =>
        new(null, error, exception, true);

    public static ProjectionAccessMembershipResult NotAuthoritative() =>
        new(null, null, null, false);
}

/// <summary>
/// Why an authoritative projection membership lookup could not be trusted. Each kind maps to a
/// distinct diagnostic and to a deny decision; none of them may fall through to another source.
/// </summary>
public enum ProjectionAccessMembershipError
{
    /// <summary>The global heartbeat watermark is missing, so freshness is unknown.</summary>
    NoWatermark,

    /// <summary>The global heartbeat watermark is older than the configured staleness limit.</summary>
    StaleWatermark,

    /// <summary>The tenant is quarantined, so its projection must not be trusted.</summary>
    Quarantined,

    /// <summary>The tenant has no lifecycle state, so its projection is untrusted.</summary>
    UnknownTenantState,

    /// <summary>The tenant is not in an Active state, so its projection must not be trusted.</summary>
    NotActiveTenant,

    /// <summary>No projection row exists for this person, so they are not a member.</summary>
    NoProjection,

    /// <summary>An exception was thrown while reading the projection. The projection is untrusted.</summary>
    ProjectionFailure
}

/// <summary>
/// A membership adapter that can report an authoritative result explicitly. The composite
/// consults it before the legacy first-match router: when it is authoritative for a lookup,
/// its result is final and no other source may override it, which is what makes projection
/// mode fail closed instead of silently falling back to Tenant membership.
/// </summary>
public interface IProjectionMembershipAdapter : IAccessMembershipAdapter
{
    /// <summary>
    /// Performs the authoritative lookup and returns the full result, including whether this
    /// adapter is authoritative for the requested scope. When <see cref="ProjectionAccessMembershipResult.IsAuthoritative"/>
    /// is true the caller must not fall through to another membership source.
    /// </summary>
    Task<ProjectionAccessMembershipResult> TryFindAsync(AccessScope scope, Guid userId, CancellationToken ct = default);
}