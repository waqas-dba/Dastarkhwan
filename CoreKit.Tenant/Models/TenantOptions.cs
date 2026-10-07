namespace CoreKit.Tenant.Settings;

public sealed class TenantOptions
{
    public const string SectionName = "Tenant";

    /// <summary>Slugs nobody may take, because they usually mean something to the platform.</summary>
    public static readonly IReadOnlyList<string> DefaultReservedSlugs = new[]
    {
        "www", "api", "app", "admin", "root", "system", "platform", "support", "help", "status",
        "mail", "static", "assets", "cdn", "auth", "login", "signup", "dashboard", "billing", "docs", "blog"
    };

    /// <summary>Added to <see cref="DefaultReservedSlugs"/>.</summary>
    public List<string> AdditionalReservedSlugs { get; set; } = new();

    public int MaxSettingsPerTenant { get; set; } = 200;

    /// <summary>How long a tenant's id, slug and status are remembered. 0 turns the cache off.</summary>
    public int InfoCacheSeconds { get; set; } = 30;

    public TenantResolutionOptions Resolution { get; set; } = new();

    public IReadOnlyCollection<string> GetReservedSlugs()
        => DefaultReservedSlugs
            .Concat(AdditionalReservedSlugs.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim().ToLowerInvariant()))
            .ToList();
}

public sealed class TenantResolutionOptions
{
    /// <summary>
    /// Off by default. A client can send any header, so turn this on only when a reverse proxy removes the
    /// header from incoming requests and sets it itself.
    /// </summary>
    public bool TrustHeader { get; set; } = false;

    public string HeaderName { get; set; } = "X-Tenant-Id";

    public string ClaimType { get; set; } = TenantClaimTypes.TenantId;
}