namespace CoreKit.IAM.Hooks;

/// <summary>
/// Lets another module add claims to the access token without IAM knowing about it.
/// Register with services.AddScoped&lt;IAccessTokenClaimsContributor, YourContributor&gt;().
/// Claims named sub, jti, email, name, role or permission are ignored: only IAM sets those.
/// </summary>
public interface IAccessTokenClaimsContributor
{
    Task<IReadOnlyCollection<Claim>> GetClaimsAsync(User user, CancellationToken ct = default);
}