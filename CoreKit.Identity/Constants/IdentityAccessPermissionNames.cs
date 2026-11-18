namespace CoreKit.Identity.Constants;

/// <summary>
/// The identity user-management permission names. These are the only permissions Identity itself
/// declares; the host maps them to authorization decisions through IIdentityAdminAuthorizer.
/// </summary>
public static class IdentityAccessPermissionNames
{
    public const string UsersRead = "identity.users.read";
    public const string UsersCreate = "identity.users.create";
    public const string UsersUpdate = "identity.users.update";
    public const string UsersDelete = "identity.users.delete";

    public sealed record PermissionDefinition(string Name, string Description);

    public static IReadOnlyList<PermissionDefinition> All { get; } =
    [
        new(UsersRead, "View users"),
        new(UsersCreate, "Create users"),
        new(UsersUpdate, "Edit users"),
        new(UsersDelete, "Delete users")
    ];
}
