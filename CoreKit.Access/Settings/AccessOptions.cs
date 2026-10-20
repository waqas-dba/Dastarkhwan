namespace CoreKit.Access.Settings;

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

    /// <summary>The claim that holds the user id. IAM issues it as "sub".</summary>
    public string UserIdClaimType { get; set; } = "sub";
}