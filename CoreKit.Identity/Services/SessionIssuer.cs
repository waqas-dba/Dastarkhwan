using System.Security.Claims;
using CoreKit.Access.Abstractions;
using CoreKit.Identity.Common;
using CoreKit.Identity.Entities;
using CoreKit.Identity.Interfaces;
using CoreKit.Identity.Models;

namespace CoreKit.Identity.Services;

public sealed class SessionIssuer : ISessionIssuer
{
    private static readonly AccessScope PlatformScope = AccessScope.ForPlatform(AccessScope.PlatformId);

    private readonly IAccessService _access;
    private readonly ITokenService _tokens;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IEnumerable<IAccessTokenClaimsContributor> _contributors;
    private readonly TimeProvider _time;

    public SessionIssuer(
        IAccessService access,
        ITokenService tokens,
        IRefreshTokenRepository refreshTokenRepository,
        IEnumerable<IAccessTokenClaimsContributor> contributors,
        TimeProvider time)
    {
        _access = access;
        _tokens = tokens;
        _refreshTokenRepository = refreshTokenRepository;
        _contributors = contributors;
        _time = time;
    }

    public Task<IdentityResult<LoginResponse>> IssueForLoginAsync(
        User user, int expectedSecurityVersion, CancellationToken ct = default)
        => IssueCoreAsync(user, expectedSecurityVersion, requireUnlocked: true, IdentityErrors.InvalidCredentials, ct);

    public Task<IdentityResult<LoginResponse>> IssueForRefreshAsync(
        User user, int expectedSecurityVersion, CancellationToken ct = default)
        => IssueCoreAsync(user, expectedSecurityVersion, requireUnlocked: false, IdentityErrors.InvalidRefreshToken, ct);

    private async Task<IdentityResult<LoginResponse>> IssueCoreAsync(
        User user,
        int expectedSecurityVersion,
        bool requireUnlocked,
        Common.IdentityError failure,
        CancellationToken ct)
    {
        // Roles and permissions come from Access (platform scope) for the response body only; they are
        // not embedded in the token, so a stale token cannot grant access.
        var access = await _access.GetAccessAsync(user.Id, PlatformScope, ct);

        var extraClaims = new List<Claim>();
        foreach (var contributor in _contributors)
            extraClaims.AddRange(await contributor.GetClaimsAsync(user.Id, access.Roles, access.Permissions, ct));

        var accessToken = _tokens.CreateAccessToken(user, extraClaims);
        var refreshToken = _tokens.CreateRefreshToken();
        var now = _time.GetUtcNow().UtcDateTime;

        // The only refresh-token insertion route: the guarded transactional insert re-checks the
        // user row under a lock held until commit, so no session can be created from stale
        // credential state. On refusal no token row exists and no response is produced.
        var issuance = await _refreshTokenRepository.TryIssueGuardedAsync(
            user.Id,
            expectedSecurityVersion,
            requireUnlocked,
            now,
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = refreshToken.TokenHash,
                CreatedAt = now,
                ExpiresAt = refreshToken.ExpiresAt
            },
            ct);

        if (issuance != GuardedIssuanceResult.Issued)
            return failure;

        return Common.IdentityResult.Success(new LoginResponse
        {
            AccessToken = accessToken.Token,
            AccessTokenExpiresAt = accessToken.ExpiresAt,
            RefreshToken = refreshToken.RawToken,
            RefreshTokenExpiresAt = refreshToken.ExpiresAt,
            User = IdentityMapper.ToDto(user, access.Roles, access.Permissions)
        });
    }
}