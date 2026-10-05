namespace Iam.Core.Entities;

public sealed class RolePermission
{
    private RolePermission() { } // used by EF Core

    internal RolePermission(Guid roleId, Guid permissionId, DateTimeOffset grantedAtUtc)
    {
        RoleId = roleId;
        PermissionId = permissionId;
        GrantedAtUtc = grantedAtUtc;
    }

    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public DateTimeOffset GrantedAtUtc { get; private set; }
}