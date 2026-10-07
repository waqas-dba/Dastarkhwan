namespace CoreKit.Tenant.Abstractions;

public interface ITenantSettingsService
{
    Task<TenantResult<IReadOnlyList<TenantSettingDto>>> ListAsync(Guid tenantId, CancellationToken ct = default);

    Task<TenantResult<TenantSettingDto>> GetAsync(Guid tenantId, string key, CancellationToken ct = default);

    Task<TenantResult<TenantSettingDto>> SetAsync(
        Guid tenantId, string key, SetTenantSettingRequest request, CancellationToken ct = default);

    /// <summary>All or nothing: either every setting is saved or none is.</summary>
    Task<TenantResult<IReadOnlyList<TenantSettingDto>>> SetManyAsync(
        Guid tenantId, SetTenantSettingsRequest request, CancellationToken ct = default);

    Task<TenantResult> RemoveAsync(Guid tenantId, string key, CancellationToken ct = default);
}