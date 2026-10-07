namespace CoreKit.Tenant.Abstractions;

public interface ITenantService
{
    /// <summary>Platform only.</summary>
    Task<TenantResult<TenantPagedResult<TenantDto>>> ListAsync(TenantListQuery query, CancellationToken ct = default);

    Task<TenantResult<TenantDto>> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>The tenant of the current request.</summary>
    Task<TenantResult<TenantDto>> GetCurrentAsync(CancellationToken ct = default);

    /// <summary>Platform only.</summary>
    Task<TenantResult<TenantDto>> CreateAsync(CreateTenantRequest request, CancellationToken ct = default);

    /// <summary>A tenant may rename itself. Changing the slug is platform only.</summary>
    Task<TenantResult<TenantDto>> UpdateAsync(Guid id, UpdateTenantRequest request, CancellationToken ct = default);

    /// <summary>Platform only. Pending or Suspended to Active.</summary>
    Task<TenantResult<TenantDto>> ActivateAsync(Guid id, CancellationToken ct = default);

    /// <summary>Platform only. Active to Suspended.</summary>
    Task<TenantResult<TenantDto>> SuspendAsync(Guid id, ChangeTenantStatusRequest request, CancellationToken ct = default);

    /// <summary>Platform only. Any other status to Archived.</summary>
    Task<TenantResult<TenantDto>> ArchiveAsync(Guid id, ChangeTenantStatusRequest request, CancellationToken ct = default);

    /// <summary>Platform only. Archived to Suspended. An administrator then activates it on purpose.</summary>
    Task<TenantResult<TenantDto>> RestoreAsync(Guid id, CancellationToken ct = default);

    /// <summary>Platform only. Permanently deletes a tenant, and only an archived one.</summary>
    Task<TenantResult> DeleteAsync(Guid id, CancellationToken ct = default);
}