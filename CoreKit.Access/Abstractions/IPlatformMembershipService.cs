namespace CoreKit.Access.Abstractions;

/// <summary>
/// Manages platform membership: add, revoke, and membership lookup. Independent of role assignments.
/// A platform member with no role assignments holds no permissions.
/// </summary>
public interface IPlatformMembershipService
{
    /// <summary>Adds a user as a platform member, or reactivates a revoked membership with a new generation.</summary>
    Task<PlatformMemberResult> AddMemberAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Revokes platform membership. Role assignments remain but become inactive.</summary>
    Task<PlatformMemberResult> RevokeMemberAsync(Guid userId, CancellationToken ct = default);

    /// <summary>True when the user is an active (non-revoked) platform member.</summary>
    Task<bool> IsMemberAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>Result of a platform membership operation.</summary>
public sealed record PlatformMemberResult(bool IsSuccess, string? ErrorCode = null, string? Message = null)
{
    public static PlatformMemberResult Ok() => new(true);
    public static PlatformMemberResult Fail(string errorCode, string message) => new(false, errorCode, message);
}
