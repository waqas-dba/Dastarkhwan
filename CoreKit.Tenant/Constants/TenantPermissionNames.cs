namespace CoreKit.Tenant.Constants;

public sealed record TenantPermissionDefinition(string Name, string Description);

/// <summary>
/// Permission names for tenant management. IAM roles are global, so a tenant-bound user can only ever
/// act on their own tenant (the service layer enforces that), and cross-tenant work needs <see cref="Platform"/>.
/// </summary>
public static class TenantPermissionNames
{
    public const string Read = "tenants.read";
    public const string Create = "tenants.create";
    public const string Update = "tenants.update";
    public const string ManageStatus = "tenants.manage_status";
    public const string Delete = "tenants.delete";
    public const string SettingsRead = "tenants.settings.read";
    public const string SettingsUpdate = "tenants.settings.update";
    public const string MembersRead = "tenants.members.read";
    public const string MembersManage = "tenants.members.manage";
    public const string AuditRead = "tenants.audit.read";

    /// <summary>Act across all tenants. Hold this only for platform staff.</summary>
    public const string Platform = "tenants.platform";

    public static IReadOnlyList<TenantPermissionDefinition> All { get; } = new List<TenantPermissionDefinition>
    {
        new(Read, "View tenants"),
        new(Create, "Create tenants"),
        new(Update, "Edit tenants"),
        new(ManageStatus, "Activate, suspend, archive and restore tenants"),
        new(Delete, "Permanently delete archived tenants"),
        new(SettingsRead, "View tenant settings"),
        new(SettingsUpdate, "Change tenant settings"),
        new(MembersRead, "View tenant members"),
        new(MembersManage, "Add and remove tenant members"),
        new(AuditRead, "View the tenant audit log"),
        new(Platform, "Act across all tenants (platform administrator)")
    };
}