namespace CoreKit.Access.Abstractions;

public sealed record AccessMembershipSnapshotMember(Guid UserId, bool IsActive, long MembershipGeneration);

/// <summary>Tenant's authoritative picture of one tenant at event sequence <see cref="HeadSequence"/>.</summary>
public sealed record AccessMembershipSnapshot(
    Guid TenantId,
    string TenantStatus,
    long HeadSequence,
    IReadOnlyList<AccessMembershipSnapshotMember> Members);

public enum AccessSnapshotOutcome
{
    /// <summary>Access was behind (or knew nothing); the snapshot was installed and the cursor advanced.</summary>
    Installed = 1,

    /// <summary>Same sequence but different content; Access was corrected to match the snapshot.</summary>
    Repaired = 2,

    /// <summary>Access already matches.</summary>
    Unchanged = 3,

    /// <summary>The snapshot is older than what Access has applied. Ignored: Access never moves backwards.</summary>
    IgnoredStale = 4,

    /// <summary>A temporary failure (for example a concurrent consumer). Try again later.</summary>
    RetryLater = 5,

    /// <summary>Invalid snapshot, or the tenant is deleted in Access.</summary>
    Rejected = 6
}

/// <summary>
/// Replaces a tenant's projection (members and lifecycle state) with an authoritative snapshot, in one
/// transaction, and advances the stream cursor to the snapshot's head. Events at or below the head that arrive
/// later are acknowledged as duplicates. Never moves the cursor backwards.
/// </summary>
public interface IAccessMembershipSnapshotInstaller
{
    Task<AccessSnapshotOutcome> InstallAsync(AccessMembershipSnapshot snapshot, CancellationToken ct = default);
}

public sealed record AccessProjectedMember(Guid UserId, bool IsActive, long MembershipGeneration);

public sealed record AccessTenantProjectionView(
    Guid TenantId, long LastAppliedSequence, string? TenantStatus, IReadOnlyList<AccessProjectedMember> Members);

/// <summary>Read-only view of the projection, used by recovery and reconciliation.</summary>
public interface IAccessMembershipProjectionReader
{
    /// <summary>Null when Access holds nothing at all for the tenant.</summary>
    Task<AccessTenantProjectionView?> GetTenantViewAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Every tenant Access holds anything for. Used to detect tenants Tenant no longer has.</summary>
    Task<IReadOnlyList<Guid>> ListTenantIdsAsync(CancellationToken ct = default);
}