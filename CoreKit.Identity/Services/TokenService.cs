using CoreKit.Identity.Constants;
using CoreKit.Identity.Entities;
using CoreKit.Identity.Interfaces;
using CoreKit.Identity.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using JwtRegNames = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace CoreKit.Identity.Services;

public sealed class TokenService : ITokenService
{
    private static readonly HashSet<string> ReservedClaims = new(StringComparer.Ordinal)
    {
        "sub", "jti", "email", "name", "role", "permission", "iss", "aud", "exp", "nbf", "iat",
        IdentityClaimTypes.SecurityVersion
    };

    private readonly JwtOptions _options;
    private readonly TimeProvider _time;
    private readonly SigningCredentials _signingCredentials;
    private readonly JwtSecurityTokenHandler _handler = new();

    public TokenService(IOptions<JwtOptions> options, TimeProvider time)
    {
        _options = options.Value;
        _time = time;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public AccessTokenResult CreateAccessToken(User user, IEnumerable<Claim>? extraClaims = null)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new Claim(JwtRegNames.Sub, user.Id.ToString()),
            new Claim(JwtRegNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegNames.Email, user.Email),
            new Claim(JwtRegNames.Iss, _options.Issuer),
            new Claim(JwtRegNames.Aud, _options.Audience),
            new Claim(JwtRegNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim(JwtRegNames.Nbf, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim(JwtRegNames.Exp, new DateTimeOffset(expires).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim(IdentityClaimTypes.SecurityVersion, user.SecurityVersion.ToString(), ClaimValueTypes.Integer32)
        };

        if (!string.IsNullOrWhiteSpace(user.DisplayName))
            claims.Add(new Claim(IdentityClaimTypes.Name, user.DisplayName));

        // Other modules may add claims, but never overwrite the ones Identity owns.
        if (extraClaims is not null)
            foreach (var claim in extraClaims.Where(c => !ReservedClaims.Contains(c.Type)))
                claims.Add(new Claim(claim.Type, claim.Value));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            SigningCredentials = _signingCredentials,
            Issuer = _options.Issuer,
            Audience = _options.Audience
        };

        var token = _handler.CreateToken(tokenDescriptor);
        var tokenString = _handler.WriteToken(token);

        return new AccessTokenResult(tokenString, expires);
    }

    public GeneratedRefreshToken CreateRefreshToken()
    {
        var rawToken = OpaqueToken.Generate(64);
        var expires = _time.GetUtcNow().UtcDateTime.AddDays(_options.RefreshTokenDays);

        return new GeneratedRefreshToken(rawToken, HashRefreshToken(rawToken), expires);
    }

    public string HashRefreshToken(string rawToken) => OpaqueToken.Hash(rawToken);
}