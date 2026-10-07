namespace CoreKit.Tenant.Abstractions;

public interface ITenantMembershipService
{
    Task<TenantResult<IReadOnlyList<TenantMemberDto>>> ListMembersAsync(Guid tenantId, CancellationToken ct = default);

    Task<TenantResult<TenantMemberDto>> AddMemberAsync(
        Guid tenantId, AddTenantMemberRequest request, CancellationToken ct = default);

    /// <summary>The last owner of a tenant cannot be removed.</summary>
    Task<TenantResult> RemoveMemberAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    /// <summary>The last owner of a tenant cannot lose ownership.</summary>
    Task<TenantResult<TenantMemberDto>> SetOwnerAsync(
        Guid tenantId, Guid userId, SetTenantOwnerRequest request, CancellationToken ct = default);

    /// <summary>The tenants a user belongs to. Allowed for the user themselves or for platform administrators.</summary>
    Task<TenantResult<IReadOnlyList<TenantMembershipDto>>> ListForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Allowed for the user themselves or for platform administrators.</summary>
    Task<TenantResult> SetDefaultTenantAsync(Guid userId, Guid tenantId, CancellationToken ct = default);

    Task<bool> IsMemberAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}