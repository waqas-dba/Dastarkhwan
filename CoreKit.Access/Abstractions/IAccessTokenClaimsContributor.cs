using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace CoreKit.Access.Abstractions;

/// <summary>
/// Lets another module add claims to the access token without Identity knowing about it.
/// Register with services.AddScoped&lt;IAccessTokenClaimsContributor, YourContributor&gt;().
/// Claims named sub, jti, email, name, role or permission are ignored: only Identity sets those.
/// </summary>
public interface IAccessTokenClaimsContributor
{
    Task<IReadOnlyCollection<Claim>> GetClaimsAsync(
        Guid userId,
        IEnumerable<string> roles,
        IEnumerable<string> permissions,
        CancellationToken ct = default);
}