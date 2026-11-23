namespace CoreKit.Tenant.Abstractions;

/// <summary>A person's standing in a tenant.</summary>
public sealed record TenantMemberInfo(bool IsOwner, long MembershipGeneration = 1);

/// <summary>
/// Who belongs to a tenant. The Tenant module provides it. Other modules (for example Access) use it to
/// decide who can hold roles, without referencing Tenant. It is a plain server-side read and is never
/// exposed over HTTP.
/// </summary>
public interface ITenantMembershipReader
{
    /// <summary>Null when the user is not a member of the tenant.</summary>
    Task<TenantMemberInfo?> FindAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// The latest event sequence committed by Tenant for this tenant. Used by Access to determine
    /// whether its membership projection is current with Tenant's authoritative state.
    /// </summary>
    Task<long> GetLastEventSequenceAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// All tenants a user is or was a member of, including soft-deleted rows. Used by Access cleanup
    /// when a user is deleted from Identity, so assignments in every tenant they ever belonged to
    /// can be removed.
    /// </summary>
    Task<IReadOnlyList<(Guid TenantId, bool IsOwner, long MembershipGeneration)>> ListForUserIncludingRemovedAsync(
        Guid userId, CancellationToken ct = default);
}