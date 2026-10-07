namespace CoreKit.Tenant.Resolution;

/// <summary>
/// The tenant named in a request header (typically set by a reverse proxy from the host name).
/// Does nothing unless Tenant:Resolution:TrustHeader is true, because any client can send a header.
/// </summary>
internal sealed class HeaderTenantResolutionStrategy : ITenantResolutionStrategy
{
    private readonly TenantResolutionOptions _options;

    public HeaderTenantResolutionStrategy(IOptions<TenantOptions> options) => _options = options.Value.Resolution;

    public int Order => 200;

    public Task<TenantIdentifier?> ResolveAsync(HttpContext httpContext, CancellationToken ct = default)
    {
        if (!_options.TrustHeader)
            return Task.FromResult<TenantIdentifier?>(null);

        var value = httpContext.Request.Headers[_options.HeaderName].ToString().Trim();

        if (value.Length == 0)
            return Task.FromResult<TenantIdentifier?>(null);

        TenantIdentifier identifier = Guid.TryParse(value, out var id)
            ? new TenantIdentifier(id, null, "header")
            : new TenantIdentifier(null, value, "header");

        return Task.FromResult<TenantIdentifier?>(identifier);
    }
}