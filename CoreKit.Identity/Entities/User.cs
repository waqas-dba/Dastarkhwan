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

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Number of consecutive failed login attempts. Resets to 0 on successful login.</summary>
    public int AccessFailedCount { get; set; }

    /// <summary>Lockout end time. When set and in the future, login is refused.</summary>
    public DateTime? LockoutEnd { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}