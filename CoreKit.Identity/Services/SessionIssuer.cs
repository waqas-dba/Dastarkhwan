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
        => IssueCoreAsync(user, expectedSecurityVersion, requireUnlocked: true, IdentityErrors.InvalidCredentials, familyId: null, oldTokenId: null, ct);

    public Task<IdentityResult<LoginResponse>> IssueForRefreshAsync(
        User user, int expectedSecurityVersion, Guid familyId, CancellationToken ct = default)
        => IssueCoreAsync(user, expectedSecurityVersion, requireUnlocked: false, IdentityErrors.InvalidRefreshToken, familyId, oldTokenId: null, ct);

    public Task<IdentityResult<LoginResponse>> RotateAsync(
        User user, int expectedSecurityVersion, Guid oldTokenId, Guid familyId, CancellationToken ct = default)
        => IssueCoreAsync(user, expectedSecurityVersion, requireUnlocked: false, IdentityErrors.InvalidRefreshToken, familyId, oldTokenId, ct);

    private async Task<IdentityResult<LoginResponse>> IssueCoreAsync(
        User user,
        int expectedSecurityVersion,
        bool requireUnlocked,
        Common.IdentityError failure,
        Guid? familyId,
        Guid? oldTokenId,
        CancellationToken ct)
    {
        var accessToken = _tokens.CreateAccessToken(user);
        var refreshToken = _tokens.CreateRefreshToken();
        var now = _time.GetUtcNow().UtcDateTime;

        // Generate a new family ID for initial login; preserve the family ID for token rotation.
        var tokenFamilyId = familyId ?? Guid.NewGuid();

        // Atomic rotation when oldTokenId is provided; guarded issuance for fresh login.
        GuardedIssuanceResult issuance;
        RotationResult? rotation = null;

        if (oldTokenId.HasValue)
        {
            rotation = await _refreshTokenRepository.TryRotateAsync(
                user.Id,
                oldTokenId.Value,
                tokenFamilyId,
                expectedSecurityVersion,
                now,
                RefreshTokenRevokedReasons.Rotated,
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

            // Convert rotation result to issuance result for unified handling
            issuance = rotation switch
            {
                RotationResult.Rotated => GuardedIssuanceResult.Issued,
                RotationResult.UserUnavailable => GuardedIssuanceResult.UserUnavailable,
                RotationResult.ReplayDetected => GuardedIssuanceResult.UserUnavailable, // Treat replay as guard failure
                RotationResult.InsertFailed => GuardedIssuanceResult.InsertFailed,
                _ => GuardedIssuanceResult.UserUnavailable
            };
        }
        else
        {
            // The only refresh-token insertion route: the guarded transactional insert re-checks the
            // user row under a lock held until commit, so no session can be created from stale
            // credential state. On refusal no token row exists and no response is produced.
            issuance = await _refreshTokenRepository.TryIssueGuardedAsync(
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
        }

        if (issuance != GuardedIssuanceResult.Issued)
        {
            if (rotation == RotationResult.ReplayDetected)
                return IdentityErrors.InvalidRefreshToken;
            return failure;
        }

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
