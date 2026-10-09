using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace CoreKit.IAM.Security;

/// <summary>
/// Random, single-use tokens (refresh tokens, email verification links).
/// A plain SHA-256 is enough to store them: the value is random bytes, so there is nothing to guess.
/// Passwords are different: humans choose them, so they need a slow, salted hash.
/// </summary>
public static class OpaqueToken
{
    public static string Generate(int byteCount)
        => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(byteCount));

    public static string Hash(string rawToken)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}