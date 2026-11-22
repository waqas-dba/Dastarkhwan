using CoreKit.Identity.Interfaces;
using Microsoft.Extensions.Logging;

namespace Dastarkhwan.Web.Email;

/// <summary>DEVELOPMENT ONLY. Prints the tokens instead of mailing them. Never log tokens in production.</summary>
public sealed class ConsoleEmailSender : IIdentityEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _logger;
    private readonly DevEmailInbox _inbox;

    public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger, DevEmailInbox inbox)
    {
        _logger = logger;
        _inbox = inbox;
    }

    public Task SendEmailVerificationAsync(EmailVerificationMessage message, CancellationToken ct = default)
    {
        _inbox.Store(message.Email, message.Token);

        _logger.LogWarning(
            "DEV ONLY: verification token for {Email} is {Token} (expires {ExpiresAt:u})",
            message.Email, message.Token, message.ExpiresAt);

        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(PasswordResetMessage message, CancellationToken ct = default)
    {
        _inbox.StoreReset(message.Email, message.Token);

        _logger.LogWarning(
            "DEV ONLY: password reset token for {Email} is {Token} (expires {ExpiresAt:u})",
            message.Email, message.Token, message.ExpiresAt);

        return Task.CompletedTask;
    }
}