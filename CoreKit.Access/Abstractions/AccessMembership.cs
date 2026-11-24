namespace CoreKit.Access.Abstractions;

using CoreKit.Access.Contracts;

/// <summary>
/// A membership adapter registered alongside the composite router.
/// </summary>
public interface IAccessMembershipAdapter : IAccessMembership
{
    Task<long> GetLastEventSequenceAsync(Guid tenantId, CancellationToken ct = default);
    Task<long> GetLastAppliedSequenceAsync(Guid tenantId, CancellationToken ct = default);
}