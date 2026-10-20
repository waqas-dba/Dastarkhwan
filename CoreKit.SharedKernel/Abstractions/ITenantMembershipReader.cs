namespace CoreKit.SharedKernel;

/// <summary>A person's standing in a tenant.</summary>
public sealed record TenantMemberInfo(bool IsOwner);

/// <summary>
/// Who belongs to a tenant. The Tenant module provides it. Other modules (for example Access) use it to
/// decide who can hold roles, without referencing Tenant. It is a plain server-side read and is never
/// exposed over HTTP.
/// </summary>
public interface ITenantMembershipReader
{
    /// <summary>Null when the user is not a member of the tenant.</summary>
    Task<TenantMemberInfo?> FindAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}