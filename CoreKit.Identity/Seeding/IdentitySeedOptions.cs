namespace CoreKit.Identity.Seeding;

/// <summary>
/// The first administrator. Keep AdminPassword out of source control
/// (user-secrets or the Identity__Seed__AdminPassword environment variable).
/// </summary>
public sealed class IdentitySeedOptions
{
    public const string SectionName = "Identity:Seed";

    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }

    public string? AdminDisplayName { get; set; } = "Administrator";
}