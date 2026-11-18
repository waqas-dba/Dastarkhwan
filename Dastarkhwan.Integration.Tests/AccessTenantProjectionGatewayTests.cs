using CoreKit.Access.Abstractions;
using CoreKit.Tenant.Outbox;
using Dastarkhwan.Integration.Membership;
using Dastarkhwan.Integration.Tests.Support;
using Xunit;

namespace Dastarkhwan.Integration.Tests;

public sealed class AccessTenantProjectionGatewayTests
{
    private static (AccessTenantProjectionGateway Gateway, FakeSnapshotInstaller Installer, FakeProjectionReader Reader) Create()
    {
        var installer = new FakeSnapshotInstaller();
        var reader = new FakeProjectionReader();
        return (new AccessTenantProjectionGateway(installer, reader), installer, reader);
    }

    [Theory]
    [InlineData(AccessSnapshotOutcome.Installed, TenantProjectionInstallOutcome.Installed)]
    [InlineData(AccessSnapshotOutcome.Repaired, TenantProjectionInstallOutcome.Repaired)]
    [InlineData(AccessSnapshotOutcome.Unchanged, TenantProjectionInstallOutcome.Unchanged)]
    [InlineData(AccessSnapshotOutcome.IgnoredStale, TenantProjectionInstallOutcome.IgnoredStale)]
    [InlineData(AccessSnapshotOutcome.RetryLater, TenantProjectionInstallOutcome.RetryLater)]
    [InlineData(AccessSnapshotOutcome.Rejected, TenantProjectionInstallOutcome.Rejected)]
    public async Task Install_outcome_is_mapped_without_upgrading_it(
        AccessSnapshotOutcome access, TenantProjectionInstallOutcome expected)
    {
        var (gateway, installer, _) = Create();
        installer.Outcome = access;

        var result = await gateway.InstallAsync(NewSnapshot(), CancellationToken.None);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task Unrecognised_install_outcome_is_retryable_not_installed()
    {
        var (gateway, installer, _) = Create();
        installer.Outcome = (AccessSnapshotOutcome)99;

        var result = await gateway.InstallAsync(NewSnapshot(), CancellationToken.None);

        Assert.Equal(TenantProjectionInstallOutcome.RetryLater, result);
    }

    [Fact]
    public async Task Snapshot_tenant_status_sequence_and_tombstones_are_preserved()
    {
        var (gateway, installer, _) = Create();
        var snapshot = NewSnapshot();

        await gateway.InstallAsync(snapshot, CancellationToken.None);

        var sent = Assert.IsType<AccessMembershipSnapshot>(installer.Received);
        Assert.Equal(snapshot.TenantId, sent.TenantId);
        Assert.Equal(snapshot.Status, sent.TenantStatus);
        Assert.Equal(snapshot.HeadSequence, sent.HeadSequence);
        Assert.Equal(snapshot.Members.Count, sent.Members.Count);
        for (var i = 0; i < snapshot.Members.Count; i++)
        {
            Assert.Equal(snapshot.Members[i].UserId, sent.Members[i].UserId);
            Assert.Equal(snapshot.Members[i].IsActive, sent.Members[i].IsActive);
            Assert.Equal(snapshot.Members[i].MembershipGeneration, sent.Members[i].MembershipGeneration);
        }

        // A removed member must reach Access as a tombstone with its generation, because that generation is
        // the floor a later rejoin must exceed.
        Assert.Contains(sent.Members, m => !m.IsActive && m.MembershipGeneration == 5);
    }

    [Fact]
    public async Task Null_snapshot_is_rejected_loudly()
    {
        var (gateway, _, _) = Create();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => gateway.InstallAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task GetView_returns_null_when_access_holds_nothing()
    {
        var (gateway, _, reader) = Create();
        reader.View = null;

        Assert.Null(await gateway.GetViewAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetView_maps_sequence_status_and_members()
    {
        var (gateway, _, reader) = Create();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        reader.View = new AccessTenantProjectionView(
            tenantId, 12, AccessTenantStatuses.Active,
            new[] { new AccessProjectedMember(userId, false, 5) });

        var view = await gateway.GetViewAsync(tenantId, CancellationToken.None);

        Assert.NotNull(view);
        Assert.Equal(tenantId, view!.TenantId);
        Assert.Equal(12, view.LastAppliedSequence);
        Assert.Equal(AccessTenantStatuses.Active, view.TenantStatus);
        var member = Assert.Single(view.Members);
        Assert.Equal(userId, member.UserId);
        Assert.False(member.IsActive);
        Assert.Equal(5, member.MembershipGeneration);
    }

    [Fact]
    public async Task ListKnownTenantIds_passes_through()
    {
        var (gateway, _, reader) = Create();
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        reader.TenantIds = ids;

        Assert.Equal(ids, await gateway.ListKnownTenantIdsAsync(CancellationToken.None));
    }

    private static TenantProjectionSnapshot NewSnapshot() => new(
        Guid.NewGuid(),
        "Active",
        20,
        new[]
        {
            new TenantProjectionSnapshotMember(Guid.NewGuid(), true, 2),
            new TenantProjectionSnapshotMember(Guid.NewGuid(), false, 5)
        });
}