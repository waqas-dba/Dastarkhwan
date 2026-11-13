namespace CoreKit.Tenant.Entities;

/// <summary>
/// Links a user to a tenant. UserId points at an Identity user but has no foreign key,
/// so the tenant database and the Identity database stay independent.
/// </summary>
public sealed class TenantMember
{
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public bool IsOwner { get; set; }

    /// <summary>The tenant a user lands in when they belong to several.</summary>
    public bool IsDefault { get; set; }

    public DateTime JoinedAt { get; set; }

    /// <summary>Membership epoch: incremented when the member is removed and re-created, so old
    /// Access role assignments (issued under an earlier generation) stay inactive after rejoin.</summary>
    public long MembershipGeneration { get; set; } = 1;

    /// <summary>Set when the membership is removed; null while active. Soft-delete keeps the
    /// generation durable across remove/rejoin so Access can enforce it.</summary>
    public DateTime? RemovedAt { get; set; }

    public TenantEntity Tenant { get; set; } = null!;
}