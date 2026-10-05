namespace Iam.Core.Constants;

public sealed record PermissionDefinition(string Code, string Description);

/// <summary>
/// Permissions that protect IAM itself. Other apps (orders, menu, ...) define their own
/// codes and register them with IAM, so IAM never needs to know about them.
/// </summary>
public static class IamPermissions
{
    public const string UsersView = "iam.users.view";
    public const string UsersCreate = "iam.users.create";
    public const string UsersUpdate = "iam.users.update";
    public const string UsersDisable = "iam.users.disable";
    public const string UsersAssignRoles = "iam.users.assign_roles";

    public const string RolesView = "iam.roles.view";
    public const string RolesManage = "iam.roles.manage";

    public const string PermissionsView = "iam.permissions.view";
    public const string PermissionsRegister = "iam.permissions.register";

    public const string SessionsView = "iam.sessions.view";
    public const string SessionsRevoke = "iam.sessions.revoke";

    public const string ClientsManage = "iam.clients.manage";

    public static IReadOnlyList<PermissionDefinition> All { get; } = new List<PermissionDefinition>
    {
        new(UsersView, "View users"),
        new(UsersCreate, "Create users"),
        new(UsersUpdate, "Edit users"),
        new(UsersDisable, "Disable and enable users"),
        new(UsersAssignRoles, "Assign roles to users"),
        new(RolesView, "View roles"),
        new(RolesManage, "Create, edit and delete roles"),
        new(PermissionsView, "View permissions"),
        new(PermissionsRegister, "Register permissions for an application"),
        new(SessionsView, "View signed-in sessions"),
        new(SessionsRevoke, "Sign users out of sessions"),
        new(ClientsManage, "Manage client applications")
    };
}