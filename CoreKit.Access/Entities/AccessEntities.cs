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

    public List<AccessRolePermission> Permissions { get; set; } = new();
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
    /// <summary>The acting user was authenticated.</summary>
    public const string ActorTypeUser = "User";

    /// <summary>A trusted system or deployment operation acted, not a user.</summary>
    public const string ActorTypeSystem = "System";

    /// <summary>The actor identity cannot be established (for example historical rows).</summary>
    public const string ActorTypeUnknown = "Unknown";

    public Guid Id { get; set; }

    public string ScopeType { get; set; } = null!;

    public Guid ScopeId { get; set; }

    public string Action { get; set; } = null!;

    public Guid? ActorUserId { get; set; }

    /// <summary>User, System or Unknown.</summary>
    public string ActorType { get; set; } = null!;

    /// <summary>Trusted origin of a system action (for example a deployment bootstrap command).
    /// Never secrets, credentials or tokens.</summary>
    public string? ActorSource { get; set; }

    public string? Details { get; set; }

    public DateTime OccurredAt { get; set; }

    /// <summary>Historical classification used by the additive audit-actor migration:
    /// rows that already carry an actor user id become User; rows without one become Unknown.</summary>
    public static string ClassifyHistoricalActor(Guid? actorUserId) =>
        actorUserId is null ? ActorTypeUnknown : ActorTypeUser;

    /// <summary>Rejects actor metadata that is not one of the permitted combinations.</summary>
    public static void ValidateActor(string actorType, Guid? actorUserId, string? actorSource)
    {
        switch (actorType)
        {
            case ActorTypeUser:
                if (actorUserId is null || actorUserId == Guid.Empty)
                    throw new ArgumentException("A User actor requires a non-empty actor user id.", nameof(actorUserId));
                if (!string.IsNullOrEmpty(actorSource))
                    throw new ArgumentException("A User actor must not carry an actor source.", nameof(actorSource));
                break;
            case ActorTypeSystem:
                if (actorUserId is not null)
                    throw new ArgumentException("A System actor must not carry an actor user id.", nameof(actorUserId));
                if (string.IsNullOrWhiteSpace(actorSource))
                    throw new ArgumentException("A System actor requires a non-empty actor source.", nameof(actorSource));
                break;
            case ActorTypeUnknown:
                if (actorUserId is not null)
                    throw new ArgumentException("An Unknown actor must not carry an actor user id.", nameof(actorUserId));
                if (actorSource is not null)
                    throw new ArgumentException("An Unknown actor must not carry an actor source.", nameof(actorSource));
                break;
            default:
                throw new ArgumentException($"Unknown actor type '{actorType}'.", nameof(actorType));
        }
    }
}