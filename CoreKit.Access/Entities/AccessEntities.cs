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

    public long MembershipGeneration { get; set; }

    public DateTime AssignedAt { get; set; }

    public Guid? AssignedBy { get; set; }

    public AccessRole Role { get; set; } = null!;
}

/// <summary>Independent platform membership record. Exists separately from role assignments;
/// a platform member with no assignments holds no permissions.</summary>
public sealed class AccessPlatformMember
{
    public Guid UserId { get; set; }

    public Guid PlatformId { get; set; }

    public long MembershipGeneration { get; set; }

    public DateTime JoinedAt { get; set; }

    public DateTime? RevokedAt { get; set; }
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

/// <summary>
/// Access-owned projection of tenant membership facts, synchronized from Tenant-published events in later slices.
/// This projection stores membership status and sequencing metadata only; it does not grant Access permissions by itself.
/// </summary>
public sealed class AccessMembershipProjection
{
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public AccessMembershipProjectionStatus Status { get; set; }

    public long MembershipGeneration { get; set; }

    public long LastAppliedSequence { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public enum AccessMembershipProjectionStatus
{
    Active = 1,
    Removed = 2
}

/// <summary>
/// The last event sequence applied for one tenant. It is the consumer's position in the stream and a
/// concurrency token: two consumers advancing the same tenant cannot both win.
/// </summary>
public sealed class AccessMembershipStreamCursor
{
    public Guid TenantId { get; set; }

    public long LastAppliedSequence { get; set; }

    public DateTime UpdatedAt { get; set; }
}

/// <summary>One event id already applied. At-least-once redelivery is acknowledged as a duplicate.</summary>
public sealed class AccessProcessedEvent
{
    public Guid EventId { get; set; }

    public Guid TenantId { get; set; }

    public long Sequence { get; set; }

    public string EventType { get; set; } = null!;

    public DateTime ProcessedAt { get; set; }
}

/// <summary>
/// The last heartbeat from Tenant. A missing or stale watermark makes every projection lookup fail closed,
/// so Access never authorizes from a projection it cannot trust.
/// </summary>
public sealed class AccessMembershipWatermark
{
    /// <summary>Fixed singleton id (<see cref="Guid.Empty"/>).</summary>
    public Guid Id { get; set; }

    public DateTime AsOf { get; set; }

    public DateTime ReceivedAt { get; set; }
}

/// <summary>A tenant whose event stream is stuck. Projection lookups for it deny until it is cleared.</summary>
public sealed class AccessQuarantinedTenant
{
    public Guid TenantId { get; set; }

    public DateTime Since { get; set; }
}