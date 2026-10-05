using Iam.Core.Results;
using Iam.Core.Security;

namespace Iam.Core.Entities;

/// <summary>
/// One signed-in device. The refresh token itself is never stored, only its hash,
/// so a stolen database cannot be used to sign in.
/// </summary>
public sealed class Session
{
    private Session() { } // used by EF Core

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ClientApplicationId { get; private set; }

    public string RefreshTokenHash { get; private set; } = default!;

    /// <summary>The hash from before the last refresh, kept to detect a stolen, replayed token.</summary>
    public string? PreviousRefreshTokenHash { get; private set; }

    public string? DeviceName { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset LastUsedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public string? RevokedReason { get; private set; }

    public static Session Start(
        Guid userId,
        Guid clientApplicationId,
        string refreshTokenHash,
        string? deviceName,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset now,
        TimeSpan lifetime)
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ClientApplicationId = clientApplicationId,
            RefreshTokenHash = refreshTokenHash,
            DeviceName = deviceName,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAtUtc = now,
            LastUsedAtUtc = now,
            ExpiresAtUtc = now + lifetime
        };

    public bool IsActive(DateTimeOffset now)
        => RevokedAtUtc is null && ExpiresAtUtc > now;

    /// <summary>
    /// Swaps the refresh token for a new one ("rotation"). Each token works once.
    /// If an already-used token comes back after the grace period, someone copied it, so the whole session is closed.
    /// </summary>
    public Result Rotate(
        string presentedHash,
        string newHash,
        DateTimeOffset now,
        TimeSpan lifetime,
        bool sliding,
        TimeSpan reuseGrace)
    {
        if (!IsActive(now))
            return Result.Failure(IamErrors.Sessions.Inactive);

        if (ConstantTime.AreEqual(presentedHash, RefreshTokenHash))
        {
            PreviousRefreshTokenHash = RefreshTokenHash;
            RefreshTokenHash = newHash;
            LastUsedAtUtc = now;

            if (sliding)
                ExpiresAtUtc = now + lifetime;

            return Result.Success();
        }

        if (PreviousRefreshTokenHash is not null &&
            ConstantTime.AreEqual(presentedHash, PreviousRefreshTokenHash))
        {
            // Two tabs refreshing at the same moment is normal and harmless.
            if (now - LastUsedAtUtc <= reuseGrace)
                return Result.Failure(IamErrors.Sessions.ConcurrentRefresh);

            Revoke(now, "refresh_token_reuse");
            return Result.Failure(IamErrors.Sessions.ReuseDetected);
        }

        return Result.Failure(IamErrors.Sessions.InvalidToken);
    }

    public void Revoke(DateTimeOffset now, string reason)
    {
        if (RevokedAtUtc is not null) return;

        RevokedAtUtc = now;
        RevokedReason = reason;
    }
}