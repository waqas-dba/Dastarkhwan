namespace CoreKit.Tenant.Resolution;

/// <summary>
/// The tenant a signed-in user belongs to, from the access token IAM issued. It is signed, so a client
/// cannot change it. Run the middleware after authentication.
/// </summary>
internal sealed class ClaimTenantResolutionStrategy : ITenantResolutionStrategy
{
    private readonly TenantResolutionOptions _options;

    public ClaimTenantResolutionStrategy(IOptions<TenantOptions> options) => _options = options.Value.Resolution;

    public int Order => 100;

    public Task<TenantIdentifier?> ResolveAsync(HttpContext httpContext, CancellationToken ct = default)
    {
        var user = httpContext.User;

        if (user.Identity?.IsAuthenticated != true)
            return Task.FromResult<TenantIdentifier?>(null);

        var value = user.FindFirst(_options.ClaimType)?.Value;

        TenantIdentifier? identifier = Guid.TryParse(value, out var id) && id != Guid.Empty
            ? new TenantIdentifier(id, null, "claim")
            : null;

        return Task.FromResult(identifier);
    }
}