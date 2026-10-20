namespace CoreKit.Access.Models;

public sealed class AccessRoleDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();

    /// <summary>How many people hold this role.</summary>
    public int MemberCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public sealed class AccessPermissionDto
{
    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;
}

public sealed class CreateAccessRoleRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<string> Permissions { get; set; } = new();
}

/// <summary>Every property is optional. A property that is not sent stays unchanged; an empty description clears it.</summary>
public sealed class UpdateAccessRoleRequest
{
    public string? Name { get; set; }

    public string? Description { get; set; }
}

public sealed class SetAccessRolePermissionsRequest
{
    public List<string> Permissions { get; set; } = new();
}

public sealed class SetAccessUserRolesRequest
{
    public List<Guid> RoleIds { get; set; } = new();
}