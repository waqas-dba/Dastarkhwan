
namespace CoreKit.IAM.Models;

public sealed class RoleDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    /// <summary>The Administrator role: it cannot be renamed, deleted or limited.</summary>
    public bool IsProtected { get; set; }

    public int UserCount { get; set; }

    public List<string> Permissions { get; set; } = new();
}

public sealed class CreateRoleRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public List<Guid> PermissionIds { get; set; } = new();
}

/// <summary>Every property is optional. A property that is not sent stays unchanged.</summary>
public sealed class UpdateRoleRequest
{
    public string? Name { get; set; }

    /// <summary>An empty string clears the description.</summary>
    public string? Description { get; set; }

    public bool? IsActive { get; set; }
}

public sealed class SetRolePermissionsRequest
{
    /// <summary>The complete list of permissions the role should have afterwards.</summary>
    public List<Guid> PermissionIds { get; set; } = new();
}