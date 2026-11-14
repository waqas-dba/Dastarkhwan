using System;
using System.Collections.Generic;
using System.Text;

namespace CoreKit.Identity.Entities;

public sealed class User
{
    public Guid Id { get; set; }

    public string Email { get; set; } = null!;

    /// <summary>Upper-case, trimmed copy of Email. The database enforces that it is unique.</summary>
    public string NormalizedEmail { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? DisplayName { get; set; }

    public bool IsActive { get; set; }

    public bool EmailConfirmed { get; set; }

    /// <summary>Mobile number as provided by the user (trimmed). A contact channel, not a login identifier.</summary>
    public string? Mobile { get; set; }

    /// <summary>Digits-only copy of Mobile (with optional leading '+'), used for uniqueness and lookup.</summary>
    public string? NormalizedMobile { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Number of consecutive failed login attempts. Resets to 0 on successful login.</summary>
    public int AccessFailedCount { get; set; }

    /// <summary>Lockout end time. When set and in the future, login is refused.</summary>
    public DateTime? LockoutEnd { get; set; }

    /// <summary>
    /// Concurrency guard for credential state. Incremented atomically on a successful password
    /// change, an administrator password reset and an account deactivation; read together with
    /// the password hash at login and with the user row at refresh and session issuance, so a
    /// stale credential cannot create a session. Not incremented for password rehashing or email
    /// confirmation. Access-token (JWT) validation of this version is a separate, not yet
    /// implemented task: existing access tokens stay valid until expiry.
    /// </summary>
    public int SecurityVersion { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}