using CoreKit.Access.Abstractions;
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

    private static readonly AccessScope PlatformScope = AccessScope.ForPlatform(AccessScope.PlatformId);

    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly ISessionIssuer _sessions;
    private readonly IAccessService _access;
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
        IAccessService access,
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
        _access = access;
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

        var user = await _userRepository.FindByNormalizedEmailAsync(normalizedEmail, ct);

        if (user is null)
        {
            // Spend the same time as a real check, so response time does not reveal which emails exist.
            _ = _hasher.Hash(password);
            _logger.LogInformation("Login failed.");
            return IdentityErrors.InvalidCredentials;
        }

        // The lockout state, the failure counter and the credential version are transitioned by
        // atomic, database-side statements (see IUserRepository). A snapshot taken here is never
        // used to decide a transition: the SecurityVersion observed together with the password
        // hash is carried into the authoritative transition, so a password change or reset that
        // lands after the read makes the login fail with a version conflict instead of issuing a
        // session from stale credentials.
        var now = Now();
        var check = _hasher.Verify(user.PasswordHash, password);

        if (check != PasswordCheckResult.Failed && user.IsActive)
        {
            var completion = await _userRepository.TryCompleteSuccessfulLoginAsync(
                user.Id, user.SecurityVersion, now, ct);

            if (completion == LoginCompletion.Completed)
            {
                // Only reached with the right password, so this message cannot be used to discover accounts.
                if (_emailOptions.RequireConfirmedEmail && !user.EmailConfirmed)
                {
                    _logger.LogInformation("Login blocked for user {UserId}: email not confirmed.", user.Id);
                    return IdentityEmailErrors.EmailNotConfirmed;
                }

                // Conditional rehash: requires the expected hash and version, so it can never
                // overwrite a password that changed concurrently, and it never bumps the version.
                if (check == PasswordCheckResult.SuccessRehashNeeded)
                    await _userRepository.TryRehashPasswordAsync(
                        user.Id, user.PasswordHash, user.SecurityVersion, _hasher.Hash(password), now, ct);

                // Second authoritative guard: the guarded insertion re-checks the user row under
                // a lock held through the token insert. A credential change or lockout that lands
                // after the login transition stops here — no session and no token row are produced.
                var issuance = await _sessions.IssueForLoginAsync(user, user.SecurityVersion, ct);

                if (issuance.IsFailure)
                {
                    _logger.LogInformation(
                        "Session issuance refused for user {UserId}: the account changed during login.", user.Id);
                    return IdentityErrors.InvalidCredentials;
                }

                return issuance;
            }

            if (completion is LoginCompletion.VersionConflict or LoginCompletion.UserMissing)
            {
                // Stale credential: the password or account state moved on after the read. This is
                // not a wrong password, so it must not be counted as a failed attempt.
                _logger.LogInformation("Login rejected for user {UserId}: credentials changed during login.", user.Id);
                return IdentityErrors.InvalidCredentials;
            }

            // Inactive or locked: keep the approved lockout policy — the attempt counts.
            await _userRepository.RegisterFailedLoginAttemptAsync(
                user.Id, now, lockoutEnd: now.AddMinutes(15), threshold: 5, ct);
            _logger.LogInformation("Login failed for user {UserId}.", user.Id);
            return IdentityErrors.InvalidCredentials;
        }

        // Wrong password, or an inactive account: recorded by the atomic failed-login transition
        // (FIX-03 behaviour: increments the counter, locks at five, never extends an active lockout).
        await _userRepository.RegisterFailedLoginAttemptAsync(
            user.Id, now, lockoutEnd: now.AddMinutes(15), threshold: 5, ct);

        _logger.LogInformation("Login failed for user {UserId}.", user.Id);
        return IdentityErrors.InvalidCredentials;
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

        // The refresh snapshot (user row read with the token) is stale-prone exactly like a
        // login snapshot, so issuance is guarded the same way: the version observed with the
        // token must still be current when the guarded insert executes, otherwise no new
        // session is created. A login lockout does not block refresh — the guard only checks
        // account state and credential version. The consumed token stays consumed either way.
        var issuance = await _sessions.IssueForRefreshAsync(token.User, token.User.SecurityVersion, ct);

        if (issuance.IsFailure)
        {
            _logger.LogInformation(
                "Refresh rejected for user {UserId}: credentials changed or the account is inactive.", token.UserId);
            return IdentityErrors.InvalidRefreshToken;
        }

        return issuance;
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

        var user = await _userRepository.FindByIdAsync(userId, ct);

        if (user is null || !user.IsActive)
            return IdentityErrors.NotAuthenticated;

        var access = await _access.GetAccessAsync(userId, PlatformScope, ct);

        return Common.IdentityResult.Success(IdentityMapper.ToDto(user, access.Roles, access.Permissions));
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

        // One atomic, version-guarded credential transition: the password update, the security
        // version increment and the revocation of every session commit together. If the account
        // changed after the read (a concurrent change, reset or deactivation), the conditional
        // update matches zero rows and this call fails without applying anything.
        var changed = await _userRepository.TryChangePasswordAsync(
            userId,
            user.PasswordHash,
            user.SecurityVersion,
            _hasher.Hash(request.NewPassword),
            now,
            bumpSecurityVersion: true,
            revokeSessions: true,
            ct);

        if (changed != CredentialUpdateResult.Updated)
        {
            _logger.LogInformation(
                "Password change for user {UserId} rejected: the account changed concurrently.", userId);
            return IdentityErrors.Conflict("The account was changed by another request. Please try again.");
        }

        return Common.IdentityResult.Success();
    }

    private DateTime Now() => _time.GetUtcNow().UtcDateTime;
}