using CoreKit.Identity.Interfaces;
using CoreKit.Tenant.Persistence;

namespace Dastarkhwan.Integration.Identity;

/// <summary>
/// Checks if a user can be deleted (not a tenant owner).
/// </summary>
public sealed class UserDeletionGuard : IUserDeletionGuard
{
    private readonly ITenantMemberRepository _memberRepository;

    public UserDeletionGuard(ITenantMemberRepository memberRepository)
    {
        _memberRepository = memberRepository;
    }

    public async Task<bool> CanDeleteUserAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return false;

        // Check if user is an owner of any active tenant
        var memberships = await _memberRepository.ListForUserAsync(userId, ct);
        return !memberships.Any(m => m.IsOwner && m.Tenant.Status == CoreKit.Tenant.Entities.TenantStatus.Active);
    }
}