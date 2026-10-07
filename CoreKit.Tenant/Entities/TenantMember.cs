namespace CoreKit.Tenant.Entities;

/// <summary>
/// Links a user to a tenant. UserId points at an IAM user but has no foreign key,
/// so the tenant database and the IAM database stay independent.
/// </summary>
public sealed class TenantMember
{
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public bool IsOwner { get; set; }

    /// <summary>The tenant a user lands in when they belong to several.</summary>
    public bool IsDefault { get; set; }

    public DateTime JoinedAt { get; set; }

    public TenantEntity Tenant { get; set; } = null!;
}