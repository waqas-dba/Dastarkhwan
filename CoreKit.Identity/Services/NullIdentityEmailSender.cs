using Microsoft.Extensions.Logging;

namespace CoreKit.Identity.Services;

/// <summary>Used until the host registers a real sender. It sends nothing and says so in the log.</summary>
public sealed class NullIdentityEmailSender : IIdentityEmailSender
{
    private readonly ILogger<NullIdentityEmailSender> _logger;

    public NullIdentityEmailSender(ILogger<NullIdentityEmailSender> logger) => _logger = logger;

    public Task SendEmailVerificationAsync(EmailVerificationMessage message, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "No IIdentityEmailSender is registered, so the verification email for user {UserId} was not sent.",
            message.UserId);

        return Task.CompletedTask;
    }
}