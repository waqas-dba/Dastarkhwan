namespace CoreKit.IAM.Entities;

public sealed class Role
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string NormalizedName { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}