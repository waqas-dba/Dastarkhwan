using System;
using System.Collections.Generic;
using System.Text;

namespace CoreKit.Identity.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Hash of the token. The raw token is never stored.</summary>
    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Family identifier. All tokens in a rotation chain share the same family ID.
    /// Rotating a token revokes the old one (RevokedAt set) and creates a new one with the same FamilyId.
    /// Reuse of a revoked token from a family revokes the entire family.</summary>
    public Guid FamilyId { get; set; }

    /// <summary>Reason for revocation. Null while the token is active.</summary>
    public string? RevokedReason { get; set; }

    public User User { get; set; } = null!;

    /// <summary>True while the token has neither been revoked nor expired.</summary>
    public bool IsActive(DateTime utcNow) => RevokedAt is null && ExpiresAt > utcNow;
}
