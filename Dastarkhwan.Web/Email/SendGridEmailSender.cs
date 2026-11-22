using CoreKit.Identity.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace Dastarkhwan.Web.Email;

/// <summary>
/// SendGrid email sender for SaaS deployments.
/// </summary>
public sealed class SendGridEmailSender : IIdentityEmailSender
{
    private readonly ILogger<SendGridEmailSender> _logger;
    private readonly SendGridOptions _options;
    private readonly string _baseUrl;
    private readonly HttpClient _httpClient;

    public SendGridEmailSender(ILogger<SendGridEmailSender> logger, IOptions<EmailOptions> options, HttpClient httpClient)
    {
        _logger = logger;
        _options = options.Value.SendGrid;
        _baseUrl = options.Value.BaseUrl.TrimEnd('/');
        _httpClient = httpClient;
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
        var payload = new
        {
            personalizations = new[]
            {
                new
                {
                    to = new[] { new { email = toEmail } },
                    subject = subject
                }
            },
            from = new { email = _options.FromEmail, name = _options.FromName },
            content = new[]
            {
                new { type = "text/html", value = body }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send")
        {
            Headers =
            {
                Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey)
            },
            Content = JsonContent.Create(payload)
        };

        try
        {
            var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"SendGrid API error: {response.StatusCode} - {error}");
            }
            _logger.LogInformation("Email sent to {Email} via SendGrid: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email} via SendGrid", toEmail);
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
                <p>Hello {message.DisplayName ?? message.Email},</p>
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
                <p>Hello {message.DisplayName ?? message.Email},</p>
                <p>You requested a password reset. Click the link below to set a new password:</p>
                <p><a href='{resetUrl}'>Reset Password</a></p>
                <p>This link expires at {message.ExpiresAt:u} UTC.</p>
                <p>If you didn't request this, you can ignore this email.</p>
            </body>
            </html>";
    }
}