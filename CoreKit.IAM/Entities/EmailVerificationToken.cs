namespace CoreKit.IAM.Entities;

public sealed class EmailVerificationToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Hash of the token. The raw token is only ever sent to the user's mailbox.</summary>
    public string TokenHash { get; set; } = null!;

    /// <summary>
    /// The address this token confirms, copied from the user when the token was issued.
    /// If the user's email changes afterwards, the token stops working.
    /// </summary>
    public string NormalizedEmail { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public User User { get; set; } = null!;
}