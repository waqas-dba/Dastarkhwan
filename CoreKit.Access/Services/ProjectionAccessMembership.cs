using System.Diagnostics.Metrics;
using CoreKit.Access.Settings;

namespace CoreKit.Access.Services;

/// <summary>
/// Tenant membership read from the Access-owned projection, gated by the freshness watermark and the tenant's
/// lifecycle state. It never calls Tenant. A positive answer is given only when ALL of these hold; otherwise it
/// fails closed (null):
///   1. projection mode is selected,
///   2. the scope is a tenant scope,
///   3. a heartbeat exists and is no older than MaxMembershipStalenessSeconds,
///   4. the tenant is not quarantined,
///   5. the tenant's lifecycle state is known and Active,
///   6. the projection holds an Active row for the user.
/// A tenant member is never an Access owner: Access permissions come only from explicit role assignments.
/// </summary>
internal sealed class ProjectionAccessMembership : IAccessMembershipAdapter
{
    private static readonly Meter Meter = new("CoreKit.Access.Membership");

    private static readonly Counter<long> UntrustedCounter = Meter.CreateCounter<long>(
        "access.membership.untrusted",
        description: "Tenant-scoped lookups denied because the projection could not be trusted (tag: reason).");

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

    public async Task<AccessMember?> FindAsync(AccessScope scope, Guid userId, CancellationToken ct = default)
    {
        var options = _options.Value;

        if (options.MembershipSource != AccessMembershipSource.Projection)
            return null;

        if (scope.Type != AccessScope.TenantType || scope.Id == Guid.Empty || userId == Guid.Empty)
            return null;

        var watermark = await _freshness.GetWatermarkAsync(ct);
        if (watermark is null)
            return Untrusted("no_heartbeat");

        var age = _time.GetUtcNow().UtcDateTime - watermark.AsOf;
        if (age > TimeSpan.FromSeconds(options.MaxMembershipStalenessSeconds))
            return Untrusted("stale_heartbeat");

        if (await _freshness.IsQuarantinedAsync(scope.Id, ct))
            return Untrusted("quarantined");

        // Same rule as the HTTP middleware: only an Active tenant authorizes. This also covers background jobs.
        var tenantStatus = await _tenantStates.GetStatusAsync(scope.Id, ct);
        if (tenantStatus is null)
            return Untrusted("tenant_state_unknown");

        if (tenantStatus != AccessTenantStatuses.Active)
            return Untrusted("tenant_not_active");

        var row = await _projections.FindReadOnlyAsync(scope.Id, userId, ct);
        if (row is null || row.Status != AccessMembershipProjectionStatus.Active)
            return null;

        return new AccessMember(false, row.MembershipGeneration);
    }

    private static AccessMember? Untrusted(string reason)
    {
        UntrustedCounter.Add(1, new KeyValuePair<string, object?>("reason", reason));
        return null;
    }
}