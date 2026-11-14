using CoreKit.Identity.Entities;

namespace CoreKit.Identity.Interfaces;

/// <summary>Persistence for users. Nothing outside the repositories should touch the DbContext.</summary>
public interface IUserRepository
{
    /// <summary>Read-only, without roles.</summary>
    Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Read-only, with roles.</summary>
    Task<User?> FindByIdWithRolesAsync(Guid id, CancellationToken ct = default);

    /// <summary>Tracked, with roles. Use it when the caller will change the user and call UpdateAsync.</summary>
    Task<User?> FindForUpdateAsync(Guid id, CancellationToken ct = default);

    /// <summary>Read-only, with roles.</summary>
    Task<User?> FindByNormalizedEmailWithRolesAsync(string normalizedEmail, CancellationToken ct = default);

    /// <summary>The id of an active user with this email whose address is not confirmed yet.</summary>
    Task<Guid?> FindUnconfirmedActiveIdByEmailAsync(string normalizedEmail, CancellationToken ct = default);

    Task<bool> EmailExistsAsync(string normalizedEmail, Guid? excludingUserId = null, CancellationToken ct = default);

    Task<bool> MobileExistsAsync(string normalizedMobile, Guid? excludingUserId = null, CancellationToken ct = default);

    Task<(IReadOnlyList<User> Items, int TotalCount)> ListAsync(
        string? search, bool? isActive, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Ids of active users holding the given active role.</summary>
    Task<IReadOnlyList<Guid>> GetActiveUserIdsInRoleAsync(string normalizedRoleName, CancellationToken ct = default);

    /// <summary>Returns false when another user already has the same normalized email.</summary>
    Task<bool> CreateAsync(User user, CancellationToken ct = default);

    /// <summary>Saves changes made to a tracked user. Returns false on a duplicate email.</summary>
    Task<bool> UpdateAsync(User user, CancellationToken ct = default);

    Task SetPasswordHashAsync(Guid userId, string passwordHash, DateTime now, CancellationToken ct = default);

    /// <summary>
    /// Atomically records a failed login attempt as one database-side state transition.
    /// Increments the failure counter, establishes the lockout when the threshold is reached,
    /// preserves an existing active lockout (its end time is never extended or replaced), and
    /// starts a fresh failure budget when the previous lockout has expired. The decision is
    /// made by the database against the current row, so a stale snapshot cannot clear a
    /// newer lockout. Returns false when the user row no longer exists.
    /// </summary>
    Task<bool> RegisterFailedLoginAttemptAsync(
        Guid userId, DateTime now, DateTime lockoutEnd, int threshold, CancellationToken ct = default);

    /// <summary>
    /// Authoritative conditional success transition, executed as a single database statement:
    /// resets the failure counter and clears the lockout only when the account is active and
    /// not currently locked, evaluated against the current row. Returns false when the
    /// transition must not be applied (user missing, inactive, or locked by a concurrent
    /// request); the caller must not issue a session.
    /// </summary>
    Task<bool> TryCompleteSuccessfulLoginAsync(Guid userId, DateTime now, CancellationToken ct = default);

    /// <summary>Makes the user's role links match exactly the given role ids.</summary>
    Task ReplaceRolesAsync(User user, IReadOnlyCollection<Guid> roleIds, CancellationToken ct = default);

    Task DeleteAsync(User user, CancellationToken ct = default);
}