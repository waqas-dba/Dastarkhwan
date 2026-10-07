namespace CoreKit.Tenant.Isolation;

/// <summary>Put this on every entity that belongs to one tenant. TenantAwareDbContext does the rest.</summary>
public interface ITenantScoped
{
    Guid TenantId { get; set; }
}