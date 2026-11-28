namespace CoreKit.Tenant.Settings;

public sealed class TenantOptions
{
    public const string SectionName = "Tenant";

    /// <summary>
    /// When true, tenant lifecycle and membership changes also write a durable outbox message in the same
    /// transaction. Leave it false (the default) unless an external consumer reads those messages: the
    /// in-process Integration handlers do not use the outbox, so with nothing consuming the rows they would
    /// only accumulate. The per-tenant event sequence and the concurrency stamp that serialize concurrent
    /// membership changes (last-owner protection) are updated either way.
    /// </summary>
    public bool PersistOutboxMessages { get; set; }

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

    /// <summary>How long a user's tenant membership is remembered. 0 turns the cache off.</summary>
    public int MembershipCacheSeconds { get; set; } = 30;

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

    /// <summary>
    /// Path prefixes that should skip tenant resolution entirely (e.g., "/account", "/platform", "/health").
    /// </summary>
    public IReadOnlyList<string> ExcludedPathPrefixes { get; set; } = new[]
    {
        "/identity",
        "/tenants/me",
        "/health",
        "/favicon.ico",
        "/.well-known"
    };
}