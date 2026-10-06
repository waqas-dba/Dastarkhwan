using CoreKit.IAM.Interfaces;

namespace CoreKit.IAM.SampleHost;



/// <summary>DEVELOPMENT ONLY. Prints the token instead of mailing it. Never log tokens in production.</summary>
public sealed class ConsoleEmailSender : IIamEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _logger;

    public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) => _logger = logger;

    public Task SendEmailVerificationAsync(EmailVerificationMessage message, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "DEV ONLY: verification token for {Email} is {Token} (expires {ExpiresAt:u})",
            message.Email, message.Token, message.ExpiresAt);

        return Task.CompletedTask;
    }
}