using CoreKit.Access.Abstractions;
using CoreKit.Access.Persistence;
using CoreKit.Access.Services;
using CoreKit.Access.Settings;
using Microsoft.Extensions.Options;

namespace CoreKit.Access.Tests;

/// <summary>
/// Tests for <see cref="ProjectionAccessMembership"/>, the authoritative Access-side tenant membership
/// lookup. The projection is the sole authority when <see cref="AccessOptions.MembershipSource"/>
/// is <see cref="AccessMembershipSource.Projection"/>; every rejection state must deny access and
/// never fall through to Tenant membership.
/// </summary>
public sealed class ProjectionSourceSafetyTests
{
    // ---- Valid input ----

    [Fact]
    public async Task IsValid_AcceptsValidProjection()
    {
        using var env = new ProjectionTestEnv();

        var result = await env.Lookup();

        Assert.True(result.IsAuthoritative);
        Assert.True(result.IsFound);
        Assert.NotNull(result.Member);
        Assert.False(result.Member.IsOwner);
        Assert.Equal(env.Projection.MembershipGeneration, result.Member.MembershipGeneration);
    }

    // ---- Invalid input (fail-closed) ----

    [Theory]
    [InlineData("no_watermark")]
    [InlineData("stale_watermark")]
    [InlineData("quarantined")]
    [InlineData("unknown_tenant_state")]
    [InlineData("pending_tenant")]
    [InlineData("suspended_tenant")]
    [InlineData("archived_tenant")]
    [InlineData("deleted_tenant")]
    [InlineData("no_projection_row")]
    [InlineData("removed_projection_row")]
    public async Task IsValid_RejectsInvalidProjection(string scenario)
    {
        using var env = new ProjectionTestEnv();

        switch (scenario)
        {
            case "no_watermark":
                env.Freshness.Watermark = null;
                break;
            case "stale_watermark":
                env.Freshness.Watermark = new AccessMembershipWatermark
                {
                    Id = Guid.Empty,
                    AsOf = env.Time.GetUtcNow().UtcDateTime.AddSeconds(-(env.Config.MaxMembershipStalenessSeconds + 1)),
                    ReceivedAt = env.Time.GetUtcNow().UtcDateTime
                };
                break;
            case "quarantined":
                env.Freshness.IsQuarantinedResult = true;
                break;
            case "unknown_tenant_state":
                env.TenantStates.State = null;
                break;
            case "pending_tenant":
                env.TenantState.Status = AccessTenantStatuses.Pending;
                break;
            case "suspended_tenant":
                env.TenantState.Status = AccessTenantStatuses.Suspended;
                break;
            case "archived_tenant":
                env.TenantState.Status = AccessTenantStatuses.Archived;
                break;
            case "deleted_tenant":
                env.TenantState.Status = AccessTenantStatuses.Deleted;
                break;
            case "no_projection_row":
                env.Projections.Projection = null;
                break;
            case "removed_projection_row":
                env.Projections.Projection = new AccessMembershipProjection
                {
                    TenantId = env.TenantId,
                    UserId = env.UserId,
                    Status = AccessMembershipProjectionStatus.Removed,
                    MembershipGeneration = 3,
                    LastAppliedSequence = 42,
                    UpdatedAt = env.Time.GetUtcNow().UtcDateTime
                };
                break;
        }

        var result = await env.Lookup();

        Assert.True(result.IsAuthoritative);
        Assert.False(result.IsFound);
        Assert.Null(result.Member);
        Assert.True(result.IsError);
    }

    // ---- Exception handling ----

    [Fact]
    public async Task IsValid_RejectsOnProjectionException()
    {
        using var env = new ProjectionTestEnv();
        env.Projections.Throw = true;

        var result = await env.Lookup();

        Assert.True(result.IsAuthoritative);
        Assert.False(result.IsFound);
        Assert.Null(result.Member);
        Assert.True(result.IsError);
        Assert.Equal(ProjectionAccessMembershipError.ProjectionFailure, result.Error);
        Assert.NotNull(result.Exception);
    }

    // ---- Non-tenant scope ----

    [Fact]
    public async Task IsValid_RejectsNonTenantScope()
    {
        using var env = new ProjectionTestEnv();
        var nonTenantScope = AccessScope.ForPlatform(AccessScope.PlatformId);

        var result = await env.Projection.FindAsync(nonTenantScope, env.UserId, CancellationToken.None);

        Assert.False(result.IsAuthoritative);
        Assert.Null(result.Member);
        Assert.Null(result.Error);
    }

    // ---- Non-projection mode ----

    [Fact]
    public async Task IsValid_RejectsNonProjectionMode()
    {
        using var env = new ProjectionTestEnv();
        env.Config.MembershipSource = AccessMembershipSource.TenantAdapter;

        var result = await env.Lookup();

        Assert.False(result.IsAuthoritative);
        Assert.Null(result.Member);
        Assert.Null(result.Error);
    }

    // ---- Private test environment ----

    private sealed class ProjectionTestEnv
    {
        public ProjectionTestEnv()
        {
            Time = new FixedTimeProvider(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero));
            Config = new AccessOptions
            {
                MembershipSource = AccessMembershipSource.Projection,
                MaxMembershipStalenessSeconds = 30
            };

            TenantId = Guid.NewGuid();
            UserId = Guid.NewGuid();
            Scope = AccessScope.ForTenant(TenantId);

            Watermark = new AccessMembershipWatermark
            {
                Id = Guid.Empty,
                AsOf = Time.GetUtcNow().UtcDateTime.AddSeconds(-5),
                ReceivedAt = Time.GetUtcNow().UtcDateTime
            };

            TenantState = new AccessTenantState
            {
                TenantId = TenantId,
                Status = AccessTenantStatuses.Active,
                UpdatedAt = Time.GetUtcNow().UtcDateTime
            };

            Projection = new AccessMembershipProjection
            {
                TenantId = TenantId,
                UserId = UserId,
                Status = AccessMembershipProjectionStatus.Active,
                MembershipGeneration = 3,
                LastAppliedSequence = 42,
                UpdatedAt = Time.GetUtcNow().UtcDateTime
            };

            Freshness = new FakeFreshnessRepository { Watermark = Watermark };
            Projections = new FakeProjectionRepository { Projection = Projection };
            TenantStates = new FakeTenantStateRepository { State = TenantState };

            Projection = new ProjectionAccessMembership(
                Freshness,
                Projections,
                TenantStates,
                Options.Create(Config),
                Time);
        }

        public AccessOptions Config { get; }
        public FixedTimeProvider Time { get; }
        public AccessScope Scope { get; }
        public Guid TenantId { get; }
        public Guid UserId { get; }

        public AccessMembershipWatermark Watermark { get; set; }
        public AccessTenantState TenantState { get; set; }
        public AccessMembershipProjection Projection { get; set; }

        public FakeFreshnessRepository Freshness { get; }
        public FakeProjectionRepository Projections { get; }
        public FakeTenantStateRepository TenantStates { get; }

        public ProjectionAccessMembership Projection { get; }

        public async Task<AccessMember?> Lookup(Guid? userId = null)
            => await Projection.FindAsync(Scope, userId ?? UserId, CancellationToken.None);
    }

    private sealed class FakeFreshnessRepository : IAccessMembershipFreshnessRepository
    {
        public AccessMembershipWatermark? Watermark { get; set; }
        public bool IsQuarantinedResult { get; set; }

        public Task<AccessMembershipWatermark?> GetWatermarkAsync(CancellationToken ct = default)
            => Task.FromResult(Watermark);

        public Task<bool> IsQuarantinedAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(IsQuarantinedResult);

        public Task<AccessQuarantinedTenant?> FindQuarantineAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult<AccessQuarantinedTenant?>(null);

        public Task<AccessMembershipWatermark?> FindWatermarkAsync(CancellationToken ct = default)
            => Task.FromResult(Watermark);

        public void AddWatermark(AccessMembershipWatermark watermark) => Watermark = watermark;
        public void AddQuarantine(AccessQuarantinedTenant quarantine) { }
        public void RemoveQuarantine(AccessQuarantinedTenant quarantine) { }
    }

    private sealed class FakeProjectionRepository : IAccessMembershipProjectionRepository
    {
        public AccessMembershipProjection? Projection { get; set; }
        public bool Throw { get; set; }

        public Task<AccessMembershipProjection?> FindAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        {
            if (Throw) throw new InvalidOperationException("projection database is unavailable");
            return Task.FromResult(Projection);
        }

        public Task<AccessMembershipProjection?> FindReadOnlyAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        {
            if (Throw) throw new InvalidOperationException("projection database is unavailable");
            return Task.FromResult(Projection);
        }

        public Task<IReadOnlyList<AccessMembershipProjection>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<AccessMembershipProjection>> ListByTenantForUpdateAsync(Guid tenantId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public void Add(AccessMembershipProjection projection) => throw new NotImplementedException();
        public void Remove(AccessMembershipProjection projection) => throw new NotImplementedException();
    }

    private sealed class FakeTenantStateRepository : IAccessTenantStateRepository
    {
        public AccessTenantState? State { get; set; }

        public Task<AccessTenantState?> FindAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(State);

        public Task<string?> GetStatusAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(State?.Status);

        public Task<IReadOnlyList<Guid>> ListTenantIdsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());

        public void Add(AccessTenantState state) => State = state;
    }
}