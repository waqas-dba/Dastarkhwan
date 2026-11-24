namespace CoreKit.Access.Models;

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

    /// <summary>The concurrency stamp the client last read. Required to prevent lost updates.</summary>
    public Guid ConcurrencyStamp { get; set; }
}

public sealed class SetAccessRolePermissionsRequest
{
    public List<string> Permissions { get; set; } = new();

    /// <summary>The concurrency stamp the client last read. Required to prevent lost updates.</summary>
    public Guid ConcurrencyStamp { get; set; }
}

public sealed class SetAccessUserRolesRequest
{
    public List<Guid> RoleIds { get; set; } = new();
}

public sealed class AddPlatformMemberRequest
{
    public Guid UserId { get; set; }
}

public sealed class PlatformMemberDto
{
    public Guid UserId { get; set; }

    public Guid PlatformId { get; set; }

    public long MembershipGeneration { get; set; }

    public DateTime JoinedAt { get; set; }

    public DateTime? RevokedAt { get; set; }
}