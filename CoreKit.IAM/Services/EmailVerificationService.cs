using CoreKit.IAM.Common;
using CoreKit.IAM.Entities;
using CoreKit.IAM.Interfaces;
using CoreKit.IAM.Models;
using CoreKit.IAM.Normalization;
using CoreKit.IAM.Security;
using CoreKit.IAM.Settings;
using CoreKit.IAM.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreKit.IAM.Services;

public sealed class EmailVerificationService : IEmailVerificationService
{
    // One message for every failure, so the response never says which part was wrong.
    private static readonly IamError InvalidToken = new(
        IamErrorKind.Validation,
        "email.invalid_verification_token",
        "The verification link is invalid or has expired.");

    private readonly IUserRepository _userRepository;
    private readonly IEmailVerificationTokenRepository _tokenRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IIamEmailSender _sender;
    private readonly IamEmailVerificationOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<EmailVerificationService> _logger;

    public EmailVerificationService(
        IUserRepository userRepository,
        IEmailVerificationTokenRepository tokenRepository,
        ICurrentUserService currentUser,
        IIamEmailSender sender,
        IOptions<IamEmailVerificationOptions> options,
        TimeProvider time,
        ILogger<EmailVerificationService> logger)
    {
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
        _currentUser = currentUser;
        _sender = sender;
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    public async Task<IamResult> SendToCurrentUserAsync(CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId)
            return IamErrors.NotAuthenticated;

        return await SendCoreAsync(userId, enforceCooldown: true, ct);
    }

    public Task<IamResult> SendAsync(Guid userId, CancellationToken ct = default)
        => SendCoreAsync(userId, enforceCooldown: false, ct);

    public async Task<IamResult> ResendByEmailAsync(ResendVerificationRequest request, CancellationToken ct = default)
    {
        var email = request.Email?.Trim();

        // Every path below returns success: the caller learns nothing about which emails have accounts.
        if (string.IsNullOrWhiteSpace(email) || email.Length > EmailValidator.MaxLength)
            return IamResult.Success();

        var normalized = IamNormalizer.NormalizeEmail(email);

        var userId = await _userRepository.FindUnconfirmedActiveIdByEmailAsync(normalized, ct);

        if (userId is { } id)
        {
            try
            {
                await SendCoreAsync(id, enforceCooldown: true, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A mail-provider outage must not turn into an error that reveals the account exists.
                _logger.LogError(ex, "Sending the verification email for user {UserId} failed.", id);
            }
        }

        return IamResult.Success();
    }

    public async Task<IamResult> VerifyAsync(VerifyEmailRequest request, CancellationToken ct = default)
    {
        var rawToken = request.Token?.Trim();

        if (string.IsNullOrEmpty(rawToken) || rawToken.Length > 200)
            return InvalidToken;

        var token = await _tokenRepository.FindByHashWithUserAsync(OpaqueToken.Hash(rawToken), ct);

        var now = Now();

        if (token is null ||
            token.UsedAt is not null ||
            token.ExpiresAt <= now ||
            !token.User.IsActive ||
            token.NormalizedEmail != token.User.NormalizedEmail) // the address changed after the token was issued
            return InvalidToken;

        if (!await _tokenRepository.ConfirmEmailAsync(token.Id, token.UserId, now, ct))
            return InvalidToken;

        _logger.LogInformation("Email confirmed for user {UserId}.", token.UserId);

        return IamResult.Success();
    }

    private async Task<IamResult> SendCoreAsync(Guid userId, bool enforceCooldown, CancellationToken ct)
    {
        var user = await _userRepository.FindByIdAsync(userId, ct);

        if (user is null || !user.IsActive)
            return IamErrors.NotFound("User");

        if (user.EmailConfirmed)
            return IamErrors.Conflict("The email address is already confirmed.");

        var now = Now();

        if (enforceCooldown &&
            await _tokenRepository.HasCreatedSinceAsync(userId, now.AddSeconds(-_options.ResendCooldownSeconds), ct))
            return IamErrors.Validation("A verification email was sent a moment ago. Please wait before asking again.");

        var rawToken = OpaqueToken.Generate(32);
        var expiresAt = now.AddHours(_options.TokenHours);

        await _tokenRepository.ReplaceOpenTokensAsync(new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = OpaqueToken.Hash(rawToken),
            NormalizedEmail = user.NormalizedEmail,
            CreatedAt = now,
            ExpiresAt = expiresAt
        }, now, ct);

        await _sender.SendEmailVerificationAsync(
            new EmailVerificationMessage(user.Id, user.Email, user.DisplayName, rawToken, expiresAt), ct);

        return IamResult.Success();
    }

    private DateTime Now() => _time.GetUtcNow().UtcDateTime;
}