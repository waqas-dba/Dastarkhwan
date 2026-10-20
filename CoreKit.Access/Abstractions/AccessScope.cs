namespace CoreKit.Access.Abstractions;

/// <summary>
/// The place roles apply to: a type and an id. A tenant is the scope of its own roles
/// ("tenant", tenantId). A later module can use another type, for example "store", without any change here.
/// </summary>
public readonly record struct AccessScope(string Type, Guid Id)
{
    public const string TenantType = "tenant";
    public const int MaxTypeLength = 30;

    public static AccessScope ForTenant(Guid tenantId) => new(TenantType, tenantId);

    public bool IsValid => !string.IsNullOrWhiteSpace(Type) && Type.Length <= MaxTypeLength && Id != Guid.Empty;

    public override string ToString() => $"{Type}:{Id:N}";
}