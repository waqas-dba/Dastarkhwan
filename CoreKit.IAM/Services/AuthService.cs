using CoreKit.IAM.Common;
using CoreKit.IAM.Interfaces;
using CoreKit.IAM.Models;
using CoreKit.IAM.Normalization;
using CoreKit.IAM.Security;
using CoreKit.IAM.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreKit.IAM.Services;

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
    private readonly IamEmailVerificationOptions _emailOptions;
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
        IOptions<IamEmailVerificationOptions> emailOptions,
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

    public async Task<IamResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;

        if (email.Length == 0 || email.Length > MaxEmailLength ||
            password.Length == 0 || password.Length > PasswordPolicy.MaxLength)
            return IamErrors.InvalidCredentials;

        var normalizedEmail = IamNormalizer.NormalizeEmail(email);

        var user = await _userRepository.FindByNormalizedEmailWithRolesAsync(normalizedEmail, ct);

        if (user is null)
        {
            // Spend the same time as a real check, so response time does not reveal which emails exist.
            _ = _hasher.Hash(password);
            _logger.LogInformation("Login failed.");
            return IamErrors.InvalidCredentials;
        }

        var check = _hasher.Verify(user.PasswordHash, password);

        if (check == PasswordCheckResult.Failed || !user.IsActive)
        {
            _logger.LogInformation("Login failed for user {UserId}.", user.Id);
            return IamErrors.InvalidCredentials;
        }

        // Only reached with the right password, so this message cannot be used to discover accounts.
        if (_emailOptions.RequireConfirmedEmail && !user.EmailConfirmed)
        {
            _logger.LogInformation("Login blocked for user {UserId}: email not confirmed.", user.Id);
            return IamEmailErrors.EmailNotConfirmed;
        }

        if (check == PasswordCheckResult.SuccessRehashNeeded)
            await _userRepository.SetPasswordHashAsync(user.Id, _hasher.Hash(password), Now(), ct);

        return IamResult.Success(await _sessions.IssueAsync(user, ct));
    }

    public async Task<IamResult<LoginResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var rawToken = request.RefreshToken;

        if (string.IsNullOrWhiteSpace(rawToken))
            return IamErrors.InvalidRefreshToken;

        var tokenHash = _tokens.HashRefreshToken(rawToken.Trim());

        var token = await _refreshTokenRepository.FindByHashWithUserAsync(tokenHash, ct);

        if (token is null)
            return IamErrors.InvalidRefreshToken;

        var now = Now();

        if (token.RevokedAt is not null)
        {
            // A token that was already used or revoked has come back. Whoever holds it may have copied it,
            // so every session of this user is closed.
            _logger.LogWarning(
                "A revoked refresh token was presented for user {UserId}. Revoking all of the user's refresh tokens.",
                token.UserId);

            await _refreshTokenRepository.RevokeAllForUserAsync(token.UserId, now, ct);
            return IamErrors.InvalidRefreshToken;
        }

        if (token.ExpiresAt <= now)
            return IamErrors.InvalidRefreshToken;

        if (!token.User.IsActive)
        {
            await _refreshTokenRepository.RevokeAllForUserAsync(token.UserId, now, ct);
            return IamErrors.InvalidRefreshToken;
        }

        if (_emailOptions.RequireConfirmedEmail && !token.User.EmailConfirmed)
        {
            await _refreshTokenRepository.RevokeAllForUserAsync(token.UserId, now, ct);
            return IamEmailErrors.EmailNotConfirmed;
        }

        // Atomic: of two requests using the same token at the same moment, only one wins.
        if (!await _refreshTokenRepository.TryRevokeAsync(token.Id, now, ct))
            return IamErrors.InvalidRefreshToken;

        return IamResult.Success(await _sessions.IssueAsync(token.User, ct));
    }

    public async Task<IamResult> LogoutAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return IamResult.Success();

        var tokenHash = _tokens.HashRefreshToken(request.RefreshToken.Trim());

        // Signing out twice, or with an unknown token, is not an error.
        await _refreshTokenRepository.RevokeByHashAsync(tokenHash, Now(), ct);

        return IamResult.Success();
    }

    public async Task<IamResult<UserDto>> GetCurrentUserAsync(CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId)
            return IamErrors.NotAuthenticated;

        var user = await _userRepository.FindByIdWithRolesAsync(userId, ct);

        if (user is null || !user.IsActive)
            return IamErrors.NotAuthenticated;

        var access = await _accessResolver.ResolveAsync(user, ct);

        return IamResult.Success(IamMapper.ToDto(user, access.Permissions));
    }

    public async Task<IamResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId)
            return IamErrors.NotAuthenticated;

        var user = await _userRepository.FindByIdAsync(userId, ct);

        if (user is null || !user.IsActive)
            return IamErrors.NotAuthenticated;

        var currentPassword = request.CurrentPassword ?? string.Empty;

        if (currentPassword.Length > PasswordPolicy.MaxLength ||
            _hasher.Verify(user.PasswordHash, currentPassword) == PasswordCheckResult.Failed)
            return IamErrors.WrongCurrentPassword;

        var policyError = PasswordPolicy.Validate(request.NewPassword, user.Email);

        if (policyError is not null)
            return IamErrors.Validation(policyError);

        if (request.NewPassword == currentPassword)
            return IamErrors.Validation("The new password must be different from the current one.");

        var now = Now();

        await _userRepository.SetPasswordHashAsync(userId, _hasher.Hash(request.NewPassword), now, ct);

        // Every device must sign in again with the new password.
        await _refreshTokenRepository.RevokeAllForUserAsync(userId, now, ct);

        return IamResult.Success();
    }

    private DateTime Now() => _time.GetUtcNow().UtcDateTime;
}