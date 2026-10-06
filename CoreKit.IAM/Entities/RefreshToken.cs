using System;
using System.Collections.Generic;
using System.Text;

namespace CoreKit.IAM.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Hash of the token. The raw token is never stored.</summary>
    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;

    /// <summary>True while the token has neither been revoked nor expired.</summary>
    public bool IsActive(DateTime utcNow) => RevokedAt is null && ExpiresAt > utcNow;
}
