namespace Iam.Core.Entities;

public sealed class UserRole
{
    private UserRole() { } // used by EF Core

    // internal: only the User class (same assembly) can create a link, so the rules cannot be bypassed.
    internal UserRole(Guid userId, Guid roleId, DateTimeOffset assignedAtUtc)
    {
        UserId = userId;
        RoleId = roleId;
        AssignedAtUtc = assignedAtUtc;
    }

    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public DateTimeOffset AssignedAtUtc { get; private set; }
}