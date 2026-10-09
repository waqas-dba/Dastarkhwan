using CoreKit.IAM.Entities;
using CoreKit.IAM.Hooks;

namespace CoreKit.Tenant.Integration;

/// <summary>
/// Adds the user's tenant to the access token IAM issues, so every later request carries it in a signed claim.
/// A user with several tenants gets their default one. A user with none gets no claim.
/// </summary>
public sealed class TenantClaimsContributor : IAccessTokenClaimsContributor
{
    private readonly ITenantMemberRepository _members;

    public TenantClaimsContributor(ITenantMemberRepository members) => _members = members;

    public async Task<IReadOnlyCollection<Claim>> GetClaimsAsync(User user, CancellationToken ct = default)
    {
        var tenantId = await _members.FindPreferredTenantIdAsync(user.Id, ct);

        return tenantId is { } id
            ? new[] { new Claim(TenantClaimTypes.TenantId, id.ToString()) }
            : Array.Empty<Claim>();
    }
}