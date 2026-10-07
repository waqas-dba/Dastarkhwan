namespace CoreKit.Tenant.Entities;

/// <summary>
/// The tenant (organization). Named TenantEntity because a type called Tenant inside the
/// CoreKit.Tenant namespace makes the simple name ambiguous with the namespace.
/// </summary>
public sealed class TenantEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Lower-case, unique, URL- and DNS-safe identifier such as "acme-foods".</summary>
    public string Slug { get; set; } = null!;

    public TenantStatus Status { get; set; }

    public string? StatusReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime StatusChangedAt { get; set; }

    /// <summary>Changes on every update, so two people editing at the same time cannot overwrite each other.</summary>
    public Guid ConcurrencyStamp { get; set; }

    public ICollection<TenantSetting> Settings { get; set; } = new List<TenantSetting>();

    public ICollection<TenantMember> Members { get; set; } = new List<TenantMember>();
}