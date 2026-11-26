using CoreKit.Access.Contracts;
using CoreKit.Identity.Contracts;
using System.Security.Claims;

namespace Dastarkhwan.Integration.Membership;

/// <summary>
/// Adds a platform admin claim to the access token when the user is a platform administrator.
/// This allows ClaimsAccessScopeContext to determine platform scope without a blocking DB call.
/// </summary>
public sealed class PlatformAdminClaimsContributor : IAccessTokenClaimsContributor
{
    private readonly IPlatformAdministratorProvider _platformAdminProvider;

    public PlatformAdminClaimsContributor(IPlatformAdministratorProvider platformAdminProvider)
        => _platformAdminProvider = platformAdminProvider;

    public async Task<IReadOnlyCollection<Claim>> GetClaimsAsync(
        Guid userId,
        IEnumerable<string> roles,
        IEnumerable<string> permissions,
        CancellationToken ct = default)
    {
        var platformAdmins = await _platformAdminProvider.GetPlatformAdministratorUserIdsAsync(ct);

        return platformAdmins.Contains(userId)
            ? new[] { new Claim("platform_admin", "true") }
            : Array.Empty<Claim>();
    }
}