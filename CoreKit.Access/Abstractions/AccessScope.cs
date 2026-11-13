namespace CoreKit.Access.Abstractions;

/// <summary>
/// The place roles apply to: a type and an id. A tenant is the scope of its own roles
/// ("tenant", tenantId). A later module can use another type, for example "store", without any change here.
/// </summary>
public readonly record struct AccessScope(string Type, Guid Id)
{
    public const string TenantType = "tenant";
    public const string PlatformType = "platform";
    public const int MaxTypeLength = 30;

    /// <summary>Single, fixed platform identifier. All platform membership and roles use this id.</summary>
    public static readonly Guid PlatformId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static AccessScope ForTenant(Guid tenantId) => new(TenantType, tenantId);

    public static AccessScope ForPlatform(Guid platformId) => new(PlatformType, platformId);

    public bool IsValid => !string.IsNullOrWhiteSpace(Type) && Type.Length <= MaxTypeLength && Id != Guid.Empty;

    public override string ToString() => $"{Type}:{Id:N}";
}