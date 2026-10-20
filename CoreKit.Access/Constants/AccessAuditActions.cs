namespace CoreKit.Access.Constants;

public static class AccessAuditActions
{
    public const string RoleCreated = "access.role_created";
    public const string RoleUpdated = "access.role_updated";
    public const string RolePermissionsChanged = "access.role_permissions_changed";
    public const string RoleDeleted = "access.role_deleted";
    public const string RolesAssigned = "access.roles_assigned";
    public const string UserRemoved = "access.user_removed";
    public const string ScopeProvisioned = "access.scope_provisioned";
    public const string ScopeRemoved = "access.scope_removed";
}