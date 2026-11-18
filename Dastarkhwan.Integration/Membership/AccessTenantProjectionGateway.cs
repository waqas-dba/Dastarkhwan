using CoreKit.Access.Abstractions;
using CoreKit.Tenant.Outbox;

namespace Dastarkhwan.Integration.Membership;

/// <summary>
/// Connects Tenant's snapshot recovery and reconciliation to Access's public snapshot installer and projection
/// reader. It uses only those public contracts. It never touches Access persistence.
///
/// Safety properties live in Access, not here: the installer is the one that refuses to move a tenant's cursor
/// backwards (IgnoredStale), repairs same-sequence differences, and rejects invalid snapshots. This gateway only
/// translates faithfully and never upgrades an outcome. An outcome it does not recognise becomes RetryLater, so
/// it can never be mistaken for a successful installation.
/// </summary>
public sealed class AccessTenantProjectionGateway : ITenantProjectionGateway
{
    private readonly IAccessMembershipSnapshotInstaller _installer;
    private readonly IAccessMembershipProjectionReader _reader;

    public AccessTenantProjectionGateway(
        IAccessMembershipSnapshotInstaller installer,
        IAccessMembershipProjectionReader reader)
    {
        _installer = installer;
        _reader = reader;
    }

    public async Task<TenantProjectionInstallOutcome> InstallAsync(TenantProjectionSnapshot snapshot, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var forAccess = new AccessMembershipSnapshot(
            snapshot.TenantId,
            snapshot.Status,
            snapshot.HeadSequence,
            snapshot.Members
                .Select(m => new AccessMembershipSnapshotMember(m.UserId, m.IsActive, m.MembershipGeneration))
                .ToList());

        var outcome = await _installer.InstallAsync(forAccess, ct);

        return outcome switch
        {
            AccessSnapshotOutcome.Installed => TenantProjectionInstallOutcome.Installed,
            AccessSnapshotOutcome.Repaired => TenantProjectionInstallOutcome.Repaired,
            AccessSnapshotOutcome.Unchanged => TenantProjectionInstallOutcome.Unchanged,
            AccessSnapshotOutcome.IgnoredStale => TenantProjectionInstallOutcome.IgnoredStale,
            AccessSnapshotOutcome.RetryLater => TenantProjectionInstallOutcome.RetryLater,
            AccessSnapshotOutcome.Rejected => TenantProjectionInstallOutcome.Rejected,
            _ => TenantProjectionInstallOutcome.RetryLater
        };
    }

    public async Task<TenantProjectionRemoteView?> GetViewAsync(Guid tenantId, CancellationToken ct)
    {
        var view = await _reader.GetTenantViewAsync(tenantId, ct);
        if (view is null)
            return null;

        return new TenantProjectionRemoteView(
            view.TenantId,
            view.LastAppliedSequence,
            view.TenantStatus,
            view.Members
                .Select(m => new TenantProjectionSnapshotMember(m.UserId, m.IsActive, m.MembershipGeneration))
                .ToList());
    }

    public Task<IReadOnlyList<Guid>> ListKnownTenantIdsAsync(CancellationToken ct)
        => _reader.ListTenantIdsAsync(ct);
}