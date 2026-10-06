using CoreKit.IAM.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreKit.IAM.Interfaces;


public sealed record AccessTokenResult(string Token, DateTime ExpiresAt);

public sealed record GeneratedRefreshToken(string RawToken, string TokenHash, DateTime ExpiresAt);

public interface ITokenService
{
    AccessTokenResult CreateAccessToken(
    User user,
    IReadOnlyCollection<string> roles,
    IReadOnlyCollection<string> permissions,
    IEnumerable<Claim>? extraClaims = null);

    /// <summary>Creates a random token. RawToken goes to the client; only TokenHash is stored.</summary>
    GeneratedRefreshToken CreateRefreshToken();

    string HashRefreshToken(string rawToken);
}
