namespace CoreKit.Tenant.Services;

/// <summary>Forgets what is remembered about a user's tenant membership.</summary>
public interface ITenantMembershipCache
{
    /// <summary>Forgets the cached membership for one (tenant, user) pair.</summary>
    void Invalidate(Guid tenantId, Guid userId);
}