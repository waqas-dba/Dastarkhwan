namespace CoreKit.Tenant.Constants;

public static class TenantAuditActions
{
    public const string Created = "tenant.created";
    public const string Updated = "tenant.updated";
    public const string Activated = "tenant.activated";
    public const string Suspended = "tenant.suspended";
    public const string Archived = "tenant.archived";
    public const string Restored = "tenant.restored";
    public const string Deleted = "tenant.deleted";
    public const string SettingsUpdated = "tenant.settings_updated";
    public const string SettingRemoved = "tenant.setting_removed";
    public const string MemberAdded = "tenant.member_added";
    public const string MemberRemoved = "tenant.member_removed";
    public const string OwnerChanged = "tenant.owner_changed";
    public const string DefaultChanged = "tenant.default_changed";
}