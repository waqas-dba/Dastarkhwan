using System.Security.Cryptography;
using System.Text.Json;
using CoreKit.IAM.Constants;
using CoreKit.IAM.Entities;
using CoreKit.IAM.Interfaces;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CoreKit.IAM.Services;

public sealed class TokenService : ITokenService
{
    private static readonly HashSet<string> ReservedClaims = new(StringComparer.Ordinal)
    {
        "sub", "jti", "email", "name", "role", "permission", "iss", "aud", "exp", "nbf", "iat"
    };

    private readonly JwtOptions _options;
    private readonly TimeProvider _time;
    private readonly SigningCredentials _signingCredentials;
    private readonly JsonWebTokenHandler _handler = new();

    public TokenService(IOptions<JwtOptions> options, TimeProvider time)
    {
        _options = options.Value;
        _time = time;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public AccessTokenResult CreateAccessToken(
        User user,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions,
        IEnumerable<Claim>? extraClaims = null)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var payload = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            [JwtRegisteredClaimNames.Email] = user.Email,
            [JwtRegisteredClaimNames.Iss] = _options.Issuer,
            [JwtRegisteredClaimNames.Aud] = _options.Audience,
            [JwtRegisteredClaimNames.Iat] = new DateTimeOffset(now).ToUnixTimeSeconds(),
            [JwtRegisteredClaimNames.Nbf] = new DateTimeOffset(now).ToUnixTimeSeconds(),
            [JwtRegisteredClaimNames.Exp] = new DateTimeOffset(expires).ToUnixTimeSeconds(),
            [IamClaimTypes.Role] = roles.ToArray(),
            [IamClaimTypes.Permission] = permissions.ToArray()
        };

        if (!string.IsNullOrWhiteSpace(user.DisplayName))
            payload[IamClaimTypes.Name] = user.DisplayName;

        // Other modules may add claims, but never overwrite the ones IAM owns.
        if (extraClaims is not null)
            foreach (var claim in extraClaims.Where(c => !ReservedClaims.Contains(c.Type)))
                payload[claim.Type] = claim.Value;

        var token = _handler.CreateToken(JsonSerializer.Serialize(payload), _signingCredentials);

        return new AccessTokenResult(token, expires);
    }

    public GeneratedRefreshToken CreateRefreshToken()
    {
        var rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        var expires = _time.GetUtcNow().UtcDateTime.AddDays(_options.RefreshTokenDays);

        return new GeneratedRefreshToken(rawToken, HashRefreshToken(rawToken), expires);
    }

    /// <summary>
    /// A plain SHA-256 is enough here: the token is 64 random bytes, so there is nothing to guess.
    /// (Passwords are different: humans choose them, so they need a slow, salted hash.)
    /// </summary>
    public string HashRefreshToken(string rawToken)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}