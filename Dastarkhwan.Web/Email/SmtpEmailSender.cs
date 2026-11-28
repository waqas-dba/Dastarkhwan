using CoreKit.Identity.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Mail;
using System.Net;
using System.Text;

namespace Dastarkhwan.Web.Email;

/// <summary>
/// Production-ready SMTP email sender.
/// </summary>
public sealed class SmtpEmailSender : IIdentityEmailSender
{
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly SmtpOptions _options;
    private readonly string _baseUrl;

    public SmtpEmailSender(ILogger<SmtpEmailSender> logger, IOptions<EmailOptions> options)
    {
        _logger = logger;
        _options = options.Value.Smtp;
        _baseUrl = options.Value.BaseUrl.TrimEnd('/');
    }

    public async Task SendEmailVerificationAsync(EmailVerificationMessage message, CancellationToken ct = default)
    {
        var subject = "Verify your email address";
        var body = BuildVerificationBody(message);
        await SendAsync(message.Email, subject, body, ct);
    }

    public async Task SendPasswordResetAsync(PasswordResetMessage message, CancellationToken ct = default)
    {
        var subject = "Reset your password";
        var body = BuildResetBody(message);
        await SendAsync(message.Email, subject, body, ct);
    }

    private async Task SendAsync(string toEmail, string subject, string body, CancellationToken ct)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, _options.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            Credentials = new NetworkCredential(_options.Username, _options.Password),
            EnableSsl = _options.EnableSsl
        };

        try
        {
            await client.SendMailAsync(message, ct);
            _logger.LogInformation("Email sent to {Email} via SMTP: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email} via SMTP", toEmail);
            throw;
        }
    }

    private string BuildVerificationBody(EmailVerificationMessage message)
    {
        var verifyUrl = $"{_baseUrl}/verify-email?token={Uri.EscapeDataString(message.Token)}";
        return $@"
            <html>
            <body>
                <h2>Verify your email address</h2>
                <p>Hello {System.Net.WebUtility.HtmlEncode(message.DisplayName ?? message.Email)},</p>
                <p>Please click the link below to verify your email address:</p>
                <p><a href='{verifyUrl}'>Verify Email</a></p>
                <p>This link expires at {message.ExpiresAt:u} UTC.</p>
                <p>If you didn't create an account, you can ignore this email.</p>
            </body>
            </html>";
    }

    private string BuildResetBody(PasswordResetMessage message)
    {
        var resetUrl = $"{_baseUrl}/reset-password?token={Uri.EscapeDataString(message.Token)}";
        return $@"
            <html>
            <body>
                <h2>Reset your password</h2>
                <p>Hello {System.Net.WebUtility.HtmlEncode(message.DisplayName ?? message.Email)},</p>
                <p>You requested a password reset. Click the link below to set a new password:</p>
                <p><a href='{resetUrl}'>Reset Password</a></p>
                <p>This link expires at {message.ExpiresAt:u} UTC.</p>
                <p>If you didn't request this, you can ignore this email.</p>
            </body>
            </html>";
    }
}