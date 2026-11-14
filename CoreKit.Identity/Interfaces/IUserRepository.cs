using CoreKit.Identity.Entities;

namespace CoreKit.Identity.Interfaces;

/// <summary>Persistence for users. Nothing outside the repositories should touch the DbContext.</summary>
public interface IUserRepository
{
    /// <summary>Read-only, without roles.</summary>
    Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Read-only, without roles.</summary>
    Task<User?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default);

    /// <summary>Tracked. Use it when the caller will change the user and call UpdateAsync.</summary>
    Task<User?> FindForUpdateAsync(Guid id, CancellationToken ct = default);

    /// <summary>The id of an active user with this email whose address is not confirmed yet.</summary>
    Task<Guid?> FindUnconfirmedActiveIdByEmailAsync(string normalizedEmail, CancellationToken ct = default);

    Task<bool> EmailExistsAsync(string normalizedEmail, Guid? excludingUserId = null, CancellationToken ct = default);

    Task<bool> MobileExistsAsync(string normalizedMobile, Guid? excludingUserId = null, CancellationToken ct = default);

    Task<(IReadOnlyList<User> Items, int TotalCount)> ListAsync(
        string? search, bool? isActive, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Of the given ids, those whose account is active. Used to intersect with Access role holders.</summary>
    Task<IReadOnlyList<Guid>> GetActiveUserIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);

    /// <summary>Returns false when another user already has the same normalized email.</summary>
    Task<bool> CreateAsync(User user, CancellationToken ct = default);

    /// <summary>Saves changes made to a tracked user. Returns false on a duplicate email.</summary>
    Task<bool> UpdateAsync(User user, CancellationToken ct = default);

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
    /// resets the failure counter and clears the lockout only when the account is active,
    /// not currently locked, and its <see cref="User.SecurityVersion"/> still equals the
    /// version observed together with the verified password hash. Returns false when the
    /// transition must not be applied (user missing, inactive, locked by a concurrent
    /// request, or the credentials changed concurrently); the caller must not issue a session.
    /// </summary>
    Task<LoginCompletion> TryCompleteSuccessfulLoginAsync(
        Guid userId, int expectedSecurityVersion, DateTime now, CancellationToken ct = default);

    /// <summary>
    /// Atomically changes a password under an expected-version and expected-hash guard, in one
    /// database transaction. Optionally increments <see cref="User.SecurityVersion"/> and revokes
    /// every refresh token of the user (used for self-service changes and administrator resets).
    /// Rehash uses the same method with both flags off: it can never overwrite a newer password,
    /// and it never bumps the version or ends sessions. Returns how the transition ended; on
    /// failure nothing is applied.
    /// </summary>
    Task<CredentialUpdateResult> TryChangePasswordAsync(
        Guid userId,
        string expectedCurrentHash,
        int expectedSecurityVersion,
        string newHash,
        DateTime now,
        bool bumpSecurityVersion,
        bool revokeSessions,
        CancellationToken ct = default);

    /// <summary>
    /// Atomically deactivates an account under an expected-version guard, increments
    /// <see cref="User.SecurityVersion"/> and revokes every refresh token in one transaction.
    /// Returns how the transition ended; on failure nothing is applied.
    /// </summary>
    Task<CredentialUpdateResult> TryDeactivateAsync(
        Guid userId, int expectedSecurityVersion, DateTime now, CancellationToken ct = default);

    /// <summary>
    /// Conditional password rehash: writes the new hash only when the expected current hash and
    /// expected <see cref="User.SecurityVersion"/> still hold, evaluated in a single statement.
    /// A stale rehash can never overwrite a newer password. It never bumps the security version
    /// and never revokes sessions. Returns false when nothing was written.
    /// </summary>
    Task<bool> TryRehashPasswordAsync(
        Guid userId,
        string expectedCurrentHash,
        int expectedSecurityVersion,
        string newHash,
        DateTime now,
        CancellationToken ct = default);

    Task DeleteAsync(User user, CancellationToken ct = default);
}

/// <summary>Outcome of the authoritative successful-login transition.</summary>
public enum LoginCompletion
{
    /// <summary>The transition was applied; the login may continue to session issuance.</summary>
    Completed,

    /// <summary>No such user any more.</summary>
    UserMissing,

    /// <summary>The account was deactivated after the credentials were read.</summary>
    Inactive,

    /// <summary>A concurrent failed request locked the account before the transition.</summary>
    Locked,

    /// <summary>
    /// The password, or the account state behind it, changed after the credentials were read
    /// (for example a concurrent password change or administrator reset). A stale credential
    /// is not a wrong password: the failed-password counter must not be incremented.
    /// </summary>
    VersionConflict
}

/// <summary>Outcome of a version-guarded credential update.</summary>
public enum CredentialUpdateResult
{
    /// <summary>The transition was applied.</summary>
    Updated,

    /// <summary>The current password hash differs from the expected one.</summary>
    HashConflict,

    /// <summary>The security version moved on after the caller read it.</summary>
    VersionConflict,

    /// <summary>No such user any more.</summary>
    UserMissing
}