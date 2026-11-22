using System.Diagnostics.Metrics;
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
    private static readonly Meter Meter = new("CoreKit.Access.Membership");

    private static readonly Counter<long> UntrustedCounter = Meter.CreateCounter<long>(
        "access.membership.untrusted",
        description: "Tenant-scoped lookups denied because the projection could not be trusted (tag: reason).");

    private readonly IAccessMembershipFreshnessRepository _freshness;
    private readonly IAccessMembershipProjectionRepository _projections;
    private readonly IAccessMembershipStreamRepository _stream;
    private readonly IAccessTenantStateRepository _tenantStates;
    private readonly IOptions<AccessOptions> _options;
    private readonly TimeProvider _time;

    public ProjectionAccessMembership(
        IAccessMembershipFreshnessRepository freshness,
        IAccessMembershipProjectionRepository projections,
        IAccessMembershipStreamRepository stream,
        IAccessTenantStateRepository tenantStates,
        IOptions<AccessOptions> options,
        TimeProvider time)
    {
        _freshness = freshness;
        _projections = projections;
        _stream = stream;
        _tenantStates = tenantStates;
        _options = options;
        _time = time;
    }

    /// <summary>
    /// Performs the authoritative lookup. When the scope is not a tenant scope, or the configured
    /// source is not Projection, this adapter is not authoritative and the caller continues to the
    /// next source. When it is authoritative, every untrusted or missing state is a deny and the
    /// caller must not fall through.
    /// </summary>
    public async Task<ProjectionAccessMembershipResult> TryFindAsync(
        AccessScope scope, Guid userId, CancellationToken ct = default)
    {
        if (scope.Type != AccessScope.TenantType)
            return ProjectionAccessMembershipResult.NotAuthoritative();

        if (_options.Value.MembershipSource != AccessMembershipSource.Projection)
            return ProjectionAccessMembershipResult.NotAuthoritative();

        // Projection mode and a tenant scope: from here on every outcome is final. An invalid
        // identity is a deny, never a fall-through to another source.
        if (scope.Id == Guid.Empty || userId == Guid.Empty)
            return ProjectionAccessMembershipResult.NotFound();

        try
        {
            var watermark = await _freshness.GetWatermarkAsync(ct);
            if (watermark is null)
                return Untrusted(ProjectionAccessMembershipError.NoWatermark, "no_heartbeat");

            var staleness = (_time.GetUtcNow().UtcDateTime - watermark.AsOf).TotalSeconds;
            if (staleness > _options.Value.MaxMembershipStalenessSeconds)
                return Untrusted(ProjectionAccessMembershipError.StaleWatermark, "stale_heartbeat");

            if (await _freshness.IsQuarantinedAsync(scope.Id, ct))
                return Untrusted(ProjectionAccessMembershipError.Quarantined, "quarantined");

            // Read-only status lookup: no change tracking on the authorization path.
            var tenantStatus = await _tenantStates.GetStatusAsync(scope.Id, ct);
            if (tenantStatus is null)
                return Untrusted(ProjectionAccessMembershipError.UnknownTenantState, "tenant_state_unknown");

            if (tenantStatus != AccessTenantStatuses.Active)
                return Untrusted(ProjectionAccessMembershipError.NotActiveTenant, "tenant_not_active");

            var projection = await _projections.FindReadOnlyAsync(scope.Id, userId, ct);
            if (projection is null || projection.Status != AccessMembershipProjectionStatus.Active)
                return ProjectionAccessMembershipResult.NotFound();

            return ProjectionAccessMembershipResult.Found(
                new AccessMember(false, projection.MembershipGeneration));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Cancellation is not a projection failure. Let the caller's cancellation propagate.
            throw;
        }
        catch (Exception ex)
        {
            return Untrusted(ProjectionAccessMembershipError.ProjectionFailure, "projection_failure", ex);
        }
    }

    /// <summary>
    /// Returns <c>null</c> for every untrusted, missing or non-authoritative state. A projection
    /// error is converted into a deny so the caller fails closed. This member exists for consumers
    /// that resolve <see cref="IAccessMembershipAdapter"/> directly; the composite uses
    /// <see cref="TryFindAsync"/> so it can tell fall-through from deny.
    /// </summary>
    public async Task<AccessMember?> FindAsync(AccessScope scope, Guid userId, CancellationToken ct = default)
    {
        var result = await TryFindAsync(scope, userId, ct);
        return result.IsFound ? result.Member : null;
    }

    public async Task<long> GetLastEventSequenceAsync(Guid tenantId, CancellationToken ct = default)
    {
        // Return the Tenant's head sequence from the last successfully installed snapshot.
        // This is the same as GetLastAppliedSequenceAsync because the cursor's LastAppliedSequence
        // is set to the snapshot's HeadSequence (the Tenant's event sequence) on installation.
        var cursor = await _stream.FindCursorAsync(tenantId, ct);
        return cursor?.LastAppliedSequence ?? 0;
    }

    public async Task<long> GetLastAppliedSequenceAsync(Guid tenantId, CancellationToken ct = default)
    {
        var cursor = await _stream.FindCursorAsync(tenantId, ct);
        return cursor?.LastAppliedSequence ?? 0;
    }

    private static ProjectionAccessMembershipResult Untrusted(
        ProjectionAccessMembershipError error, string reason, Exception? exception = null)
    {
        UntrustedCounter.Add(1, new KeyValuePair<string, object?>("reason", reason));
        return ProjectionAccessMembershipResult.Failure(error, exception);
    }
}