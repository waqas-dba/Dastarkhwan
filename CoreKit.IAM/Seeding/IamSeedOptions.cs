namespace CoreKit.IAM.Seeding;

/// <summary>
/// The first administrator. Keep AdminPassword out of source control
/// (user-secrets or the Iam__Seed__AdminPassword environment variable).
/// </summary>
public sealed class IamSeedOptions
{
    public const string SectionName = "Iam:Seed";

    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }

    public string? AdminDisplayName { get; set; } = "Administrator";
}