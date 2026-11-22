using System.Security.Claims;
using CoreKit.Identity.Common;
using CoreKit.Identity.Entities;
using CoreKit.Identity.Interfaces;
using CoreKit.Identity.Models;

namespace CoreKit.Identity.Services;

public sealed class SessionIssuer : ISessionIssuer
{
    private readonly ITokenService _tokens;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly TimeProvider _time;

    public SessionIssuer(
        ITokenService tokens,
        IRefreshTokenRepository refreshTokenRepository,
        TimeProvider time)
    {
        _tokens = tokens;
        _refreshTokenRepository = refreshTokenRepository;
        _time = time;
    }

    public Task<IdentityResult<LoginResponse>> IssueForLoginAsync(
        User user, int expectedSecurityVersion, CancellationToken ct = default)
        => IssueCoreAsync(user, expectedSecurityVersion, requireUnlocked: true, IdentityErrors.InvalidCredentials, familyId: null, ct);

    public Task<IdentityResult<LoginResponse>> IssueForRefreshAsync(
        User user, int expectedSecurityVersion, Guid familyId, CancellationToken ct = default)
        => IssueCoreAsync(user, expectedSecurityVersion, requireUnlocked: false, IdentityErrors.InvalidRefreshToken, familyId, ct);

    private async Task<IdentityResult<LoginResponse>> IssueCoreAsync(
        User user,
        int expectedSecurityVersion,
        bool requireUnlocked,
        Common.IdentityError failure,
        Guid? familyId,
        CancellationToken ct)
    {
        var accessToken = _tokens.CreateAccessToken(user);
        var refreshToken = _tokens.CreateRefreshToken();
        var now = _time.GetUtcNow().UtcDateTime;

        // Generate a new family ID for initial login; preserve the family ID for token rotation.
        var tokenFamilyId = familyId ?? Guid.NewGuid();

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
                ExpiresAt = refreshToken.ExpiresAt,
                FamilyId = tokenFamilyId
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
            User = IdentityMapper.ToDto(user)
        });
    }
}
