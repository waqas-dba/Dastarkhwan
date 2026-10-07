namespace CoreKit.Tenant.Abstractions;

public interface ITenantInfoProvider
{
    Task<TenantInfo?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<TenantInfo?> GetBySlugAsync(string slug, CancellationToken ct = default);
}