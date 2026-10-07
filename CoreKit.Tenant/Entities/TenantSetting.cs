namespace CoreKit.Tenant.Entities;

public sealed class TenantSetting
{
    public Guid TenantId { get; set; }

    /// <summary>Lower-case, for example "general.timezone".</summary>
    public string Key { get; set; } = null!;

    public string Value { get; set; } = null!;

    public DateTime UpdatedAt { get; set; }

    public TenantEntity Tenant { get; set; } = null!;
}