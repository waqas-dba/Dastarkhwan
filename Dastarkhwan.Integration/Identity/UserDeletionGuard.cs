using CoreKit.Identity.Contracts;
using CoreKit.Tenant.Entities;
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

        var memberships = await _memberRepository.ListForUserAsync(userId, ct);

        foreach (var membership in memberships)
        {
            if (!membership.IsOwner)
                continue;

            // Block if user owns an Active tenant
            if (membership.Tenant.Status == TenantStatus.Active)
                return false;

            // Block if user is the last owner of any tenant (any status)
            if (await _memberRepository.CountOwnersAsync(membership.TenantId, ct) <= 1)
                return false;
        }

        return true;
    }
}