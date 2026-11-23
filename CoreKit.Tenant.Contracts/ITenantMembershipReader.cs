namespace CoreKit.Tenant.Abstractions;

/// <summary>
/// Answers "who belongs to this tenant" for other modules through the Tenant contracts.
/// Server-side code only; never exposed over HTTP.
/// </summary>
public interface ITenantMembershipReader
{
    /// <summary>
    /// Returns membership info for a user in a tenant, or null when the user is not a member.
    /// Results may be cached for a short time; callers should not depend on real-time consistency.
    /// </summary>
    Task<TenantMemberInfo?> FindAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// All tenants a user is or was a member of, including soft-deleted rows. Used by Access cleanup
    /// when a user is deleted from Identity, so assignments in every tenant they ever belonged to
    /// can be removed.
    /// </summary>
    Task<IReadOnlyList<(Guid TenantId, bool IsOwner, long MembershipGeneration)>> ListForUserIncludingRemovedAsync(
        Guid userId, CancellationToken ct = default);
}

/// <summary>Membership information returned by <see cref="ITenantMembershipReader"/>.</summary>
public sealed record TenantMemberInfo(bool IsOwner, long MembershipGeneration = 1);
