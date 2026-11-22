namespace CoreKit.Access.Settings;

/// <summary>Where Access reads tenant membership from.</summary>
public enum AccessMembershipSource
{
    /// <summary>Legacy: ask the Tenant adapter on every lookup. No projection, no freshness gate.</summary>
    TenantAdapter = 0,

    /// <summary>Read the Access-owned projection, gated by freshness and the tenant's lifecycle state.</summary>
    Projection = 1
}

public sealed class AccessOptions
{
    public const string SectionName = "Access";

    public int MaxRolesPerScope { get; set; } = 50;

    public int MaxRolesPerUser { get; set; } = 20;

    /// <summary>
    /// How long a person's permissions are remembered. Role and assignment changes made through Access take
    /// effect at once on the same server. With several servers (or when Tenant changes an owner) it can take
    /// up to this long. 0 turns the cache off.
    /// </summary>
    public int CacheSeconds { get; set; } = 30;

    /// <summary>The claim that holds the user id. Identity issues it as "sub".</summary>
    public string UserIdClaimType { get; set; } = "sub";

    /// <summary>
    /// Where tenant membership comes from. Defaults to the Access-owned projection, gated by freshness
    /// and the tenant's lifecycle state.
    /// </summary>
    public AccessMembershipSource MembershipSource { get; set; } = AccessMembershipSource.Projection;

    /// <summary>
    /// A projection lookup is trusted only when the Tenant heartbeat is no older than this. Bounds how long a
    /// positive membership answer can be stale after a Tenant outage.
    /// </summary>
    public int MaxMembershipStalenessSeconds { get; set; } = 30;

    /// <summary>How often the Access host writes the freshness heartbeat while the projection is selected.</summary>
    public int MembershipHeartbeatSeconds { get; set; } = 30;
}