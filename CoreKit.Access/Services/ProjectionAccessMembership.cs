using CoreKit.Access.Abstractions;

namespace CoreKit.Access.Services;

/// <summary>
/// Tenant membership through Access's own projection. This is the authoritative source for
/// tenant membership when <c>Access:MembershipSource</c> is <c>Projection</c>. It answers only
/// for a tenant scope and only while the projection is trusted; every untrusted state denies
/// and is reported through <see cref="ProjectionAccessMembershipError"/> so the caller can
/// distinguish "not a member" from "cannot trust the projection".
/// </summary>
internal sealed class ProjectionAccessMembership : IProjectionMembershipAdapter
{
    private readonly IAccessMembershipFreshnessRepository _freshness;
    private readonly IAccessMembershipProjectionRepository _projections;
    private readonly IAccessTenantStateRepository _tenantStates;
    private readonly IOptions<AccessOptions> _options;
    private readonly TimeProvider _time;

    public ProjectionAccessMembership(
        IAccessMembershipFreshnessRepository freshness,
        IAccessMembershipProjectionRepository projections,
        IAccessTenantStateRepository tenantStates,
        IOptions<AccessOptions> options,
        TimeProvider time)
    {
        _freshness = freshness;
        _projections = projections;
        _tenantStates = tenantStates;
        _options = options;
        _time = time;
    }

    /// <summary>
    /// Performs the authoritative lookup. When the configured source is not Projection, or the
    /// scope is not a tenant scope, this adapter is not authoritative and the caller must fall
    /// through to the next source. When it is authoritative, every untrusted state is reported
    /// as an error and the caller must deny without falling through.
    /// </summary>
    public async Task<ProjectionAccessMembershipResult> TryFindAsync(
        AccessScope scope, Guid userId, CancellationToken ct = default)
    {
        if (scope.Type != AccessScope.TenantType || userId == Guid.Empty)
            return ProjectionAccessMembershipResult.NotAuthoritative();

        if (_options.Value.MembershipSource != AccessMembershipSource.Projection)
            return ProjectionAccessMembershipResult.NotAuthoritative();

        try
        {
            var watermark = await _freshness.GetWatermarkAsync(ct);
            if (watermark is null)
                return ProjectionAccessMembershipResult.Failure(ProjectionAccessMembershipError.NoWatermark);

            var staleness = (_time.GetUtcNow().UtcDateTime - watermark.AsOf).TotalSeconds;
            if (staleness > _options.Value.MaxMembershipStalenessSeconds)
                return ProjectionAccessMembershipResult.Failure(ProjectionAccessMembershipError.StaleWatermark);

            if (await _freshness.IsQuarantinedAsync(scope.Id, ct))
                return ProjectionAccessMembershipResult.Failure(ProjectionAccessMembershipError.Quarantined);

            var tenantState = await _tenantStates.FindAsync(scope.Id, ct);
            if (tenantState is null)
                return ProjectionAccessMembershipResult.Failure(ProjectionAccessMembershipError.UnknownTenantState);

            if (tenantState.Status != AccessTenantStatuses.Active)
                return ProjectionAccessMembershipResult.Failure(ProjectionAccessMembershipError.NotActiveTenant);

            var projection = await _projections.FindReadOnlyAsync(scope.Id, userId, ct);
            if (projection is null)
                return ProjectionAccessMembershipResult.NotFound();

            if (projection.Status != AccessMembershipProjectionStatus.Active)
                return ProjectionAccessMembershipResult.NotFound();

            return ProjectionAccessMembershipResult.Found(
                new AccessMember(false, projection.MembershipGeneration));
        }
        catch (Exception ex)
        {
            return ProjectionAccessMembershipResult.Failure(ProjectionAccessMembershipError.ProjectionFailure, ex);
        }
    }

    /// <inheritdoc/>
    /// Returns <c>null</c> for every untrusted or missing state. Never throws: a projection
    /// error is converted into a deny so the caller fails closed. This member exists only for
    /// consumers that resolve <see cref="IAccessMembershipAdapter"/> directly; the composite
    /// prefers <see cref="TryFindAsync"/> so it can distinguish fall-through from deny.
    public async Task<AccessMember?> FindAsync(AccessScope scope, Guid userId, CancellationToken ct = default)
    {
        var result = await TryFindAsync(scope, userId, ct);
        return result.IsFound ? result.Member : null;
    }
}