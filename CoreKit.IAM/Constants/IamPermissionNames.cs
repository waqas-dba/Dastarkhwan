using System;
using System.Collections.Generic;
using System.Text;

namespace CoreKit.IAM.Constants;

public sealed record PermissionSeed(string Name, string Description);

/// <summary>
/// The generic permissions IAM itself needs. Other modules define their own permission
/// names later; IAM only provides the mechanism.
/// </summary>
public static class IamPermissionNames
{
    public const string UsersRead = "users.read";
    public const string UsersCreate = "users.create";
    public const string UsersUpdate = "users.update";
    public const string UsersDelete = "users.delete";

    public const string RolesRead = "roles.read";
    public const string RolesCreate = "roles.create";
    public const string RolesUpdate = "roles.update";
    public const string RolesDelete = "roles.delete";

    public const string PermissionsRead = "permissions.read";

    public static IReadOnlyList<PermissionSeed> All { get; } = new List<PermissionSeed>
    {
        new(UsersRead, "View users"),
        new(UsersCreate, "Create users"),
        new(UsersUpdate, "Edit users"),
        new(UsersDelete, "Delete users"),
        new(RolesRead, "View roles"),
        new(RolesCreate, "Create roles"),
        new(RolesUpdate, "Edit roles"),
        new(RolesDelete, "Delete roles"),
        new(PermissionsRead, "View permissions")
    };
}
