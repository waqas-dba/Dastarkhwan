using CoreKit.Identity.Common;
using CoreKit.Identity.Interfaces;
using CoreKit.Identity.Models;
using CoreKit.Identity.Normalization;
using CoreKit.Identity.Security;
using CoreKit.Identity.Settings;
using CoreKit.Identity.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreKit.Identity.Services;

public sealed class PasswordResetService : IPasswordResetService
{
    private const int MaxTokenLength = 200;

    private readonly IUserRepository _users;
    private readonly IUserLockoutRepository _lockouts;
    private readonly IPasswordHasher _hasher;
    private readonly PasswordResetTokenProtector _protector;
    private readonly IIdentityEmailSender _sender;
    private readonly IdentityPasswordResetOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        IUserRepository users,
        IUserLockoutRepository lockouts,
        IPasswordHasher hasher,
        PasswordResetTokenProtector protector,
        IIdentityEmailSender sender,
        IOptions<IdentityPasswordResetOptions> options,
        TimeProvider time,
        ILogger<PasswordResetService> logger)
    {
        _users = users;
        _lockouts = lockouts;
        _hasher = hasher;
        _protector = protector;
        _sender = sender;
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    public async Task<Common.IdentityResult> RequestAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var email = request.Email?.Trim();

        // Every path returns success: the caller learns nothing about which emails have accounts.
        if (string.IsNullOrWhiteSpace(email) || email.Length > EmailValidator.MaxLength)
            return Common.IdentityResult.Success();

        var user = await _users.FindByNormalizedEmailAsync(IdentityNormalizer.NormalizeEmail(email), ct);

        if (user is null || !user.IsActive)
        {
            _logger.LogInformation("Password reset requested for an unknown or inactive account.");
            return Common.IdentityResult.Success();
        }

        var expiresAt = Now().AddMinutes(_options.TokenMinutes);
        var token = _protector.Create(user, expiresAt);

        try
        {
            await _sender.SendPasswordResetAsync(
                new PasswordResetMessage(user.Id, user.Email, user.DisplayName, token, expiresAt), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A mail-provider outage must not become an error that reveals the account exists.
            _logger.LogError(ex, "Sending the password reset email for user {UserId} failed.", user.Id);
        }

        return Common.IdentityResult.Success();
    }

    public async Task<Common.IdentityResult> ResetAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var rawToken = request.Token?.Trim();

        // One message for every token failure, so the response never says which part was wrong.
        if (string.IsNullOrEmpty(rawToken) || rawToken.Length > MaxTokenLength ||
            !_protector.TryReadUserId(rawToken, out var userId))
            return IdentityErrors.InvalidResetToken;

        var user = await _users.FindByIdAsync(userId, ct);
        var now = Now();

        if (user is null || !user.IsActive || !_protector.IsValid(rawToken, user, now))
            return IdentityErrors.InvalidResetToken;

        var newPassword = request.NewPassword ?? string.Empty;

        var policyError = PasswordPolicy.Validate(newPassword, user.Email);

        if (policyError is not null)
            return IdentityErrors.Validation(policyError);

        if (_hasher.Verify(user.PasswordHash, newPassword) != PasswordCheckResult.Failed)
            return IdentityErrors.Validation("The new password must be different from the current one.");

        // One atomic, version-guarded transition: the new password, the security-version bump and the
        // revocation of every session commit together. Of two concurrent resets with the same token
        // only one can match the expected hash and version; the other finds the token no longer valid.
        var changed = await _users.TryChangePasswordAsync(
            user.Id,
            user.PasswordHash,
            user.SecurityVersion,
            _hasher.Hash(newPassword),
            now,
            bumpSecurityVersion: true,
            revokeSessions: true,
            ct);

        if (changed != CredentialUpdateResult.Updated)
        {
            _logger.LogInformation("Password reset for user {UserId} rejected: the account changed concurrently.", user.Id);
            return IdentityErrors.InvalidResetToken;
        }

        try
        {
            // Best effort: the password is already changed, and a lockout expires by itself.
            await _lockouts.ClearLockoutAsync(user.Id, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Clearing the lockout after a password reset failed for user {UserId}.", user.Id);
        }

        _logger.LogInformation("Password reset completed for user {UserId}.", user.Id);

        return Common.IdentityResult.Success();
    }

    private DateTime Now() => _time.GetUtcNow().UtcDateTime;
}
