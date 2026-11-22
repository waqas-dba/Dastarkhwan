using CoreKit.Identity.Interfaces;
using CoreKit.Tenant.Abstractions;

namespace Dastarkhwan.Integration.Membership;

/// <summary>
/// Validates that a user exists and is active in Identity before allowing them to become a tenant owner.
/// </summary>
public sealed class IdentityOwnerUserValidator : IOwnerUserValidator
{
    private readonly IUserRepository _userRepository;

    public IdentityOwnerUserValidator(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<bool> ValidateOwnerUserAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return false;

        var user = await _userRepository.FindByIdAsync(userId, ct);
        return user is not null && user.IsActive;
    }
}