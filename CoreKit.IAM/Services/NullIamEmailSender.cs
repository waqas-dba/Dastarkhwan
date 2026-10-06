using Microsoft.Extensions.Logging;

namespace CoreKit.IAM.Services;

/// <summary>Used until the host registers a real sender. It sends nothing and says so in the log.</summary>
public sealed class NullIamEmailSender : IIamEmailSender
{
    private readonly ILogger<NullIamEmailSender> _logger;

    public NullIamEmailSender(ILogger<NullIamEmailSender> logger) => _logger = logger;

    public Task SendEmailVerificationAsync(EmailVerificationMessage message, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "No IIamEmailSender is registered, so the verification email for user {UserId} was not sent.",
            message.UserId);

        return Task.CompletedTask;
    }
}