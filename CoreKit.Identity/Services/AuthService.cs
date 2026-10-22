using CoreKit.Identity.Common;
using CoreKit.Identity.Interfaces;
using CoreKit.Identity.Models;
using CoreKit.Identity.Normalization;
using CoreKit.Identity.Security;
using CoreKit.Identity.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreKit.Identity.Services;

public sealed class AuthService : IAuthService
{
    // Longer input is never a real login, and hashing huge strings would be an easy way to slow the server down.
    private const int MaxEmailLength = 320;

    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly ISessionIssuer _sessions;
    private readonly IAccessResolver _accessResolver;
    private readonly ICurrentUserService _currentUser;
    private readonly IdentityEmailVerificationOptions _emailOptions;
    private readonly TimeProvider _time;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher hasher,
        ITokenService tokens,
        ISessionIssuer sessions,
        IAccessResolver accessResolver,
        ICurrentUserService currentUser,
        IOptions<IdentityEmailVerificationOptions> emailOptions,
        TimeProvider time,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _hasher = hasher;
        _tokens = tokens;
        _sessions = sessions;
        _accessResolver = accessResolver;
        _currentUser = currentUser;
        _emailOptions = emailOptions.Value;
        _time = time;
        _logger = logger;
    }

    public async Task<IdentityResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;

        if (email.Length == 0 || email.Length > MaxEmailLength ||
            password.Length == 0 || password.Length > PasswordPolicy.MaxLength)
            return IdentityErrors.InvalidCredentials;

        var normalizedEmail = IdentityNormalizer.NormalizeEmail(email);

        var user = await _userRepository.FindByNormalizedEmailWithRolesAsync(normalizedEmail, ct);

        if (user is null)
        {
            // Spend the same time as a real check, so response time does not reveal which emails exist.
            _ = _hasher.Hash(password);
            _logger.LogInformation("Login failed.");
            return IdentityErrors.InvalidCredentials;
        }

        var check = _hasher.Verify(user.PasswordHash, password);

        if (check == PasswordCheckResult.Failed || !user.IsActive)
        {
            _logger.LogInformation("Login failed for user {UserId}.", user.Id);
            return IdentityErrors.InvalidCredentials;
        }

        // Only reached with the right password, so this message cannot be used to discover accounts.
        if (_emailOptions.RequireConfirmedEmail && !user.EmailConfirmed)
        {
            _logger.LogInformation("Login blocked for user {UserId}: email not confirmed.", user.Id);
            return IdentityEmailErrors.EmailNotConfirmed;
        }

        if (check == PasswordCheckResult.SuccessRehashNeeded)
            await _userRepository.SetPasswordHashAsync(user.Id, _hasher.Hash(password), Now(), ct);

        return Common.IdentityResult.Success(await _sessions.IssueAsync(user, ct));
    }

    public async Task<IdentityResult<LoginResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var rawToken = request.RefreshToken;

        if (string.IsNullOrWhiteSpace(rawToken))
            return IdentityErrors.InvalidRefreshToken;

        var tokenHash = _tokens.HashRefreshToken(rawToken.Trim());

        var token = await _refreshTokenRepository.FindByHashWithUserAsync(tokenHash, ct);

        if (token is null)
            return IdentityErrors.InvalidRefreshToken;

        var now = Now();

        if (token.RevokedAt is not null)
        {
            // A token that was already used or revoked has come back. Whoever holds it may have copied it,
            // so every session of this user is closed.
            _logger.LogWarning(
                "A revoked refresh token was presented for user {UserId}. Revoking all of the user's refresh tokens.",
                token.UserId);

            await _refreshTokenRepository.RevokeAllForUserAsync(token.UserId, now, ct);
            return IdentityErrors.InvalidRefreshToken;
        }

        if (token.ExpiresAt <= now)
            return IdentityErrors.InvalidRefreshToken;

        if (!token.User.IsActive)
        {
            await _refreshTokenRepository.RevokeAllForUserAsync(token.UserId, now, ct);
            return IdentityErrors.InvalidRefreshToken;
        }

        if (_emailOptions.RequireConfirmedEmail && !token.User.EmailConfirmed)
        {
            await _refreshTokenRepository.RevokeAllForUserAsync(token.UserId, now, ct);
            return IdentityEmailErrors.EmailNotConfirmed;
        }

        // Atomic: of two requests using the same token at the same moment, only one wins.
        if (!await _refreshTokenRepository.TryRevokeAsync(token.Id, now, ct))
            return IdentityErrors.InvalidRefreshToken;

        return Common.IdentityResult.Success(await _sessions.IssueAsync(token.User, ct));
    }

    public async Task<Common.IdentityResult> LogoutAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Common.IdentityResult.Success();

        var tokenHash = _tokens.HashRefreshToken(request.RefreshToken.Trim());

        // Signing out twice, or with an unknown token, is not an error.
        await _refreshTokenRepository.RevokeByHashAsync(tokenHash, Now(), ct);

        return Common.IdentityResult.Success();
    }

    public async Task<IdentityResult<UserDto>> GetCurrentUserAsync(CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId)
            return IdentityErrors.NotAuthenticated;

        var user = await _userRepository.FindByIdWithRolesAsync(userId, ct);

        if (user is null || !user.IsActive)
            return IdentityErrors.NotAuthenticated;

        var access = await _accessResolver.ResolveAsync(user, ct);

        return Common.IdentityResult.Success(IdentityMapper.ToDto(user, access.Permissions));
    }

    public async Task<Common.IdentityResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId)
            return IdentityErrors.NotAuthenticated;

        var user = await _userRepository.FindByIdAsync(userId, ct);

        if (user is null || !user.IsActive)
            return IdentityErrors.NotAuthenticated;

        var currentPassword = request.CurrentPassword ?? string.Empty;

        if (currentPassword.Length > PasswordPolicy.MaxLength ||
            _hasher.Verify(user.PasswordHash, currentPassword) == PasswordCheckResult.Failed)
            return IdentityErrors.WrongCurrentPassword;

        var policyError = PasswordPolicy.Validate(request.NewPassword, user.Email);

        if (policyError is not null)
            return IdentityErrors.Validation(policyError);

        if (request.NewPassword == currentPassword)
            return IdentityErrors.Validation("The new password must be different from the current one.");

        var now = Now();

        await _userRepository.SetPasswordHashAsync(userId, _hasher.Hash(request.NewPassword), now, ct);

        // Every device must sign in again with the new password.
        await _refreshTokenRepository.RevokeAllForUserAsync(userId, now, ct);

        return Common.IdentityResult.Success();
    }

    private DateTime Now() => _time.GetUtcNow().UtcDateTime;
}