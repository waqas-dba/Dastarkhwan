namespace CoreKit.Access.Constants;

/// <summary>The permissions Access itself uses. They are added to every catalog automatically.</summary>
public static class AccessPermissionNames
{
    public const string RolesRead = "access.roles.read";
    public const string RolesManage = "access.roles.manage";
    public const string AssignmentsRead = "access.assignments.read";
    public const string AssignmentsManage = "access.assignments.manage";

    public static IReadOnlyList<AccessPermissionDefinition> All { get; } = new List<AccessPermissionDefinition>
    {
        new(RolesRead, "View roles and the permission list"),
        new(RolesManage, "Create, change and delete roles"),
        new(AssignmentsRead, "View which roles a person holds"),
        new(AssignmentsManage, "Give and remove roles")
    };
}