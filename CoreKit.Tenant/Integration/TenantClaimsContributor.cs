using CoreKit.SharedKernel.Abstractions;
using System.Security.Claims;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CoreKit.Tenant.Integration;

/// <summary>
/// Adds the user's tenant to the access token Identity issues, so every later request carries it in a signed claim.
/// A user with several tenants gets their default one. A user with none gets no claim.
/// </summary>
public sealed class TenantClaimsContributor : IAccessTokenClaimsContributor
{
    private readonly ITenantMemberRepository _members;

    public TenantClaimsContributor(ITenantMemberRepository members) => _members = members;

    public async Task<IReadOnlyCollection<Claim>> GetClaimsAsync(
        Guid userId,
        IEnumerable<string> roles,
        IEnumerable<string> permissions,
        CancellationToken ct = default)
    {
        var tenantId = await _members.FindPreferredTenantIdAsync(userId, ct);

        return tenantId is { } id
            ? new[] { new Claim(TenantClaimTypes.TenantId, id.ToString()) }
            : Array.Empty<Claim>();
    }
}
