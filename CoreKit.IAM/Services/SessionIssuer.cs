using System.Security.Claims;
using CoreKit.IAM.Entities;
using CoreKit.IAM.Hooks;
using CoreKit.IAM.Interfaces;
using CoreKit.IAM.Models;

namespace CoreKit.IAM.Services;

public sealed class SessionIssuer : ISessionIssuer
{
    private readonly IAccessResolver _accessResolver;
    private readonly ITokenService _tokens;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IEnumerable<IAccessTokenClaimsContributor> _contributors;
    private readonly TimeProvider _time;

    public SessionIssuer(
        IAccessResolver accessResolver,
        ITokenService tokens,
        IRefreshTokenRepository refreshTokenRepository,
        IEnumerable<IAccessTokenClaimsContributor> contributors,
        TimeProvider time)
    {
        _accessResolver = accessResolver;
        _tokens = tokens;
        _refreshTokenRepository = refreshTokenRepository;
        _contributors = contributors;
        _time = time;
    }

    public async Task<LoginResponse> IssueAsync(User user, CancellationToken ct = default)
    {
        var access = await _accessResolver.ResolveAsync(user, ct);

        var extraClaims = new List<Claim>();
        foreach (var contributor in _contributors)
            extraClaims.AddRange(await contributor.GetClaimsAsync(user, ct));

        var accessToken = _tokens.CreateAccessToken(user, access.Roles, access.Permissions, extraClaims);
        var refreshToken = _tokens.CreateRefreshToken();

        await _refreshTokenRepository.AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshToken.TokenHash,
            CreatedAt = _time.GetUtcNow().UtcDateTime,
            ExpiresAt = refreshToken.ExpiresAt
        }, ct);

        return new LoginResponse
        {
            AccessToken = accessToken.Token,
            AccessTokenExpiresAt = accessToken.ExpiresAt,
            RefreshToken = refreshToken.RawToken,
            RefreshTokenExpiresAt = refreshToken.ExpiresAt,
            User = IamMapper.ToDto(user, access.Permissions)
        };
    }
}