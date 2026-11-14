using CoreKit.Identity.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreKit.Identity.Interfaces;


public sealed record AccessTokenResult(string Token, DateTime ExpiresAt);

public sealed record GeneratedRefreshToken(string RawToken, string TokenHash, DateTime ExpiresAt);

public interface ITokenService
{
    /// <summary>
    /// Creates the signed access token. Roles and permissions are no longer embedded: Access is the
    /// authority and reads them from the database per request, so a stale token cannot grant access.
    /// </summary>
    AccessTokenResult CreateAccessToken(User user, IEnumerable<Claim>? extraClaims = null);

    /// <summary>Creates a random token. RawToken goes to the client; only TokenHash is stored.</summary>
    GeneratedRefreshToken CreateRefreshToken();

    string HashRefreshToken(string rawToken);
}