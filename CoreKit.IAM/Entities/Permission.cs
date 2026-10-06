using System;
using System.Collections.Generic;
using System.Text;

namespace CoreKit.IAM.Entities;

public sealed class Permission
{
    public Guid Id { get; set; }

    /// <summary>Lower-case dotted name such as "users.read".</summary>
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
