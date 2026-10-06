using Microsoft.Extensions.Logging;

namespace CoreKit.IAM.Services;

public sealed class EmailVerificationService : IEmailVerificationService
{
    // One message for every failure, so the response never says which part was wrong.
    private static readonly IamError InvalidToken = new(
        IamErrorKind.Validation,
        "email.invalid_verification_token",
        "The verification link is invalid or has expired.");

    private readonly IamDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IIamEmailSender _sender;
    private readonly IamEmailVerificationOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<EmailVerificationService> _logger;

    public EmailVerificationService(
        IamDbContext db,
        ICurrentUserService currentUser,
        IIamEmailSender sender,
        IOptions<IamEmailVerificationOptions> options,
        TimeProvider time,
        ILogger<EmailVerificationService> logger)
    {
        _db = db;
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

        var userId = await _db.Users
            .AsNoTracking()
            .Where(u => u.NormalizedEmail == normalized && u.IsActive && !u.EmailConfirmed)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(ct);

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

        var tokenHash = Hash(rawToken);

        var token = await _db.EmailVerificationTokens
            .AsNoTracking()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        var now = Now();

        if (token is null ||
            token.UsedAt is not null ||
            token.ExpiresAt <= now ||
            !token.User.IsActive ||
            token.NormalizedEmail != token.User.NormalizedEmail) // the address changed after the token was issued
            return InvalidToken;

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        // Atomic: if the link is opened twice at the same moment, only one request gets the token.
        var claimed = await _db.EmailVerificationTokens
            .Where(t => t.Id == token.Id && t.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, (DateTime?)now), ct);

        if (claimed == 0)
            return InvalidToken;

        await _db.Users
            .Where(u => u.Id == token.UserId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.EmailConfirmed, true)
                .SetProperty(u => u.UpdatedAt, (DateTime?)now), ct);

        await transaction.CommitAsync(ct);

        _logger.LogInformation("Email confirmed for user {UserId}.", token.UserId);

        return IamResult.Success();
    }

    private async Task<IamResult> SendCoreAsync(Guid userId, bool enforceCooldown, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null || !user.IsActive)
            return IamErrors.NotFound("User");

        if (user.EmailConfirmed)
            return IamErrors.Conflict("The email address is already confirmed.");

        var now = Now();

        if (enforceCooldown)
        {
            var cutoff = now.AddSeconds(-_options.ResendCooldownSeconds);

            if (await _db.EmailVerificationTokens.AnyAsync(t => t.UserId == userId && t.CreatedAt > cutoff, ct))
                return IamErrors.Validation("A verification email was sent a moment ago. Please wait before asking again.");
        }

        // Only the newest link works: earlier unused ones are closed.
        await _db.EmailVerificationTokens
            .Where(t => t.UserId == userId && t.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, (DateTime?)now), ct);

        var rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var expiresAt = now.AddHours(_options.TokenHours);

        _db.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Hash(rawToken),
            NormalizedEmail = user.NormalizedEmail,
            CreatedAt = now,
            ExpiresAt = expiresAt
        });

        await _db.SaveChangesAsync(ct);

        await _sender.SendEmailVerificationAsync(
            new EmailVerificationMessage(user.Id, user.Email, user.DisplayName, rawToken, expiresAt), ct);

        return IamResult.Success();
    }

    /// <summary>The token is 32 random bytes, so a plain SHA-256 is enough (same reasoning as refresh tokens).</summary>
    private static string Hash(string rawToken)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private DateTime Now() => _time.GetUtcNow().UtcDateTime;
}