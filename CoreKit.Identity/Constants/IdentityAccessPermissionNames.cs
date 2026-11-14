namespace CoreKit.Identity.Constants;

/// <summary>
/// The identity user-management permissions declared in the Access catalog. These are the only
/// permissions Identity itself needs; role and permission management now lives in Access
/// (access.roles.*, access.assignments.*). Identity checks them through IPermissionChecker
/// against the fixed platform scope, never from JWT claims.
/// </summary>
public static class IdentityAccessPermissionNames
{
    public const string UsersRead = "identity.users.read";
    public const string UsersCreate = "identity.users.create";
    public const string UsersUpdate = "identity.users.update";
    public const string UsersDelete = "identity.users.delete";

    public static IReadOnlyList<(string Name, string Description)> All { get; } = new List<(string, string)>
    {
        (UsersRead, "View users"),
        (UsersCreate, "Create users"),
        (UsersUpdate, "Edit users"),
        (UsersDelete, "Delete users")
    };
}