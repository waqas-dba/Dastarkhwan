using CoreKit.Access.Abstractions;
using CoreKit.SharedKernel;

namespace CoreKit.Tenant.Integration;

/// <summary>
/// Exposes Tenant membership to Access through the public <see cref="IAccessMembership"/> contract.
/// A tenant member is a member of the scope (can hold Access roles) but is NOT an Access owner:
/// Access permissions come only from explicit Access role assignments, never from Tenant ownership.
/// </summary>
public sealed class TenantAccessMembership : IAccessMembership
{
    private readonly ITenantMembershipReader _membershipReader;

    public TenantAccessMembership(ITenantMembershipReader membershipReader) => _membershipReader = membershipReader;

    public async Task<AccessMember?> FindAsync(AccessScope scope, Guid userId, CancellationToken ct = default)
    {
        if (scope.Type != AccessScope.TenantType || userId == Guid.Empty)
            return null;

        var member = await _membershipReader.FindAsync(scope.Id, userId, ct);

        // Tenant membership makes the user a scope member (they can hold roles), but never an Access owner.
        // AccessMember.IsOwner == true would grant every catalog permission; Tenant ownership must not trigger that.
        return member is null ? null : new AccessMember(false);
    }
}