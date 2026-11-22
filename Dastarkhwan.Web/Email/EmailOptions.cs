namespace Dastarkhwan.Web.Email;

/// <summary>
    /// Configuration options for email sending in different environments.
    /// </summary>
    public sealed class EmailOptions
    {
        public const string SectionName = "Email";

        /// <summary>
        /// Email provider: "Console" (development), "Smtp" (production), "SendGrid" (SaaS), "Mailgun" (SaaS), etc.
        /// </summary>
        public string Provider { get; set; } = "Console";

        /// <summary>
        /// Base URL for verification/reset links (e.g., "https://app.example.com").
        /// </summary>
        public string BaseUrl { get; set; } = "http://localhost:5000";

        /// <summary>
        /// SMTP settings (used when Provider = "Smtp").
        /// </summary>
        public SmtpOptions Smtp { get; set; } = new();

        /// <summary>
        /// SendGrid settings (used when Provider = "SendGrid").
        /// </summary>
        public SendGridOptions SendGrid { get; set; } = new();

        /// <summary>
        /// Mailgun settings (used when Provider = "Mailgun").
        /// </summary>
        public MailgunOptions Mailgun { get; set; } = new();

        /// <summary>Valid email provider values.</summary>
        public static readonly string[] ValidProviders = { "Console", "Smtp", "SendGrid", "Mailgun" };
    }

/// <summary>SMTP configuration for production email sending.</summary>
public sealed class SmtpOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
    public string FromEmail { get; set; } = "noreply@example.com";
    public string FromName { get; set; } = "Dastarkhwan";
}

/// <summary>SendGrid configuration for SaaS email sending.</summary>
public sealed class SendGridOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "noreply@example.com";
    public string FromName { get; set; } = "Dastarkhwan";
}

/// <summary>Mailgun configuration for SaaS email sending.</summary>
public sealed class MailgunOptions
{
    public string Domain { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "noreply@example.com";
    public string FromName { get; set; } = "Dastarkhwan";
}