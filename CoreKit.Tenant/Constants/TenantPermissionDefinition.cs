namespace CoreKit.Tenant.Constants;

/// <summary>
/// A permission definition owned by Tenant. Registered into Access through Integration.
/// </summary>
public sealed record TenantPermissionDefinition(string Name, string Description);