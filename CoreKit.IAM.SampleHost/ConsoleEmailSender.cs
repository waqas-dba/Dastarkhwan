using System.Collections.Concurrent;
using CoreKit.IAM.Interfaces;

/// <summary>DEVELOPMENT ONLY. Remembers the latest verification token per email so tests can read it.</summary>
public sealed class DevEmailInbox
{
    private readonly ConcurrentDictionary<string, string> _tokens = new(StringComparer.OrdinalIgnoreCase);

    public void Store(string email, string token) => _tokens[email] = token;

    public string? Get(string email) => _tokens.TryGetValue(email, out var token) ? token : null;
}

/// <summary>DEVELOPMENT ONLY. Prints the token instead of mailing it. Never log tokens in production.</summary>
public sealed class ConsoleEmailSender : IIamEmailSender
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
}