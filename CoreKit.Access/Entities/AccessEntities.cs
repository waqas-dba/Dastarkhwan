namespace CoreKit.Access.Entities;

/// <summary>A named set of permissions inside one scope (for example one tenant).</summary>
public sealed class AccessRole
{
    public Guid Id { get; set; }

    public string ScopeType { get; set; } = null!;

    public Guid ScopeId { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Upper-case name, so "cashier" and "Cashier" count as the same role.</summary>
    public string NormalizedName { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Changes on every update, so two people editing at the same time cannot overwrite each other.</summary>
    public Guid ConcurrencyStamp { get; set; }

    public ICollection<AccessRolePermission> Permissions { get; set; } = new List<AccessRolePermission>();
}

public sealed class AccessRolePermission
{
    public Guid RoleId { get; set; }

    /// <summary>Lower-case dotted name such as "sales.create". Owned by the application, not by Access.</summary>
    public string Permission { get; set; } = null!;

    public AccessRole Role { get; set; } = null!;
}

/// <summary>A person holding a role in a scope. UserId has no foreign key: users live in another module.</summary>
public sealed class AccessAssignment
{
    public Guid RoleId { get; set; }

    public Guid UserId { get; set; }

    public string ScopeType { get; set; } = null!;

    public Guid ScopeId { get; set; }

    public DateTime AssignedAt { get; set; }

    public Guid? AssignedBy { get; set; }

    public AccessRole Role { get; set; } = null!;
}

/// <summary>One recorded change. No foreign key, so history survives when a scope is deleted.</summary>
public sealed class AccessAuditEntry
{
    public Guid Id { get; set; }

    public string ScopeType { get; set; } = null!;

    public Guid ScopeId { get; set; }

    public string Action { get; set; } = null!;

    public Guid? ActorUserId { get; set; }

    public string? Details { get; set; }

    public DateTime OccurredAt { get; set; }
}