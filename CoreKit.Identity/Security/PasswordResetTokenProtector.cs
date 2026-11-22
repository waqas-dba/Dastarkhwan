using System.Security.Cryptography;
using System.Text;
using CoreKit.Identity.Entities;
using Microsoft.AspNetCore.WebUtilities;

namespace CoreKit.Identity.Security;

/// <summary>
/// Creates and validates stateless password-reset tokens: "{userId}.{expiryUnixSeconds}.{mac}".
/// The MAC covers the user id, the expiry, the user's current SecurityVersion and a hash of the current
/// password hash, so a token stops working the moment the password changes, the version moves on, or the
/// password is rehashed. That makes every token single-use without storing anything.
/// The MAC key is derived from the JWT signing key with a purpose label, so it is never the JWT key itself.
/// </summary>
public sealed class PasswordResetTokenProtector
{
    private const string Purpose = "dk-identity-password-reset-v1";

    private readonly byte[] _key;

    public PasswordResetTokenProtector(string signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
            throw new ArgumentException("A signing key is required.", nameof(signingKey));

        _key = HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), Encoding.UTF8.GetBytes(Purpose));
    }

    public string Create(User user, DateTime expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(user);

        var expiry = new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();
        var payload = $"{user.Id:N}.{expiry}";

        return $"{payload}.{WebEncoders.Base64UrlEncode(ComputeMac(payload, user))}";
    }

    /// <summary>Reads the user id from a token's shape only. It proves nothing; call <see cref="IsValid"/> next.</summary>
    public bool TryReadUserId(string token, out Guid userId)
    {
        userId = Guid.Empty;

        var parts = token.Split('.');

        return parts.Length == 3 &&
               Guid.TryParseExact(parts[0], "N", out userId) &&
               userId != Guid.Empty;
    }

    /// <summary>True only when the token was issued for this user, has not expired, and the user's credentials are unchanged.</summary>
    public bool IsValid(string token, User user, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(user);

        var parts = token.Split('.');

        if (parts.Length != 3 ||
            !Guid.TryParseExact(parts[0], "N", out var tokenUserId) ||
            tokenUserId != user.Id ||
            !long.TryParse(parts[1], out var expiry))
            return false;

        DateTime expiresAt;
        byte[] presentedMac;

        try
        {
            expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiry).UtcDateTime;
            presentedMac = WebEncoders.Base64UrlDecode(parts[2]);
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or FormatException)
        {
            return false;
        }

        if (expiresAt <= nowUtc)
            return false;

        var expectedMac = ComputeMac($"{parts[0]}.{parts[1]}", user);

        return CryptographicOperations.FixedTimeEquals(expectedMac, presentedMac);
    }

    private byte[] ComputeMac(string payload, User user)
    {
        var passwordFingerprint = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(user.PasswordHash)));
        var data = Encoding.UTF8.GetBytes($"{payload}|{user.SecurityVersion}|{passwordFingerprint}");

        return HMACSHA256.HashData(_key, data);
    }
}
