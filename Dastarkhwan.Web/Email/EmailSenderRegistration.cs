using CoreKit.Identity.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dastarkhwan.Web.Email;

/// <summary>
/// Extension methods for registering the appropriate email sender based on environment and configuration.
/// </summary>
public static class EmailSenderRegistration
{
    /// <summary>
    /// Registers the email sender based on the configured provider and environment.
    /// Development: Console (logs tokens to output)
    /// Production: Requires explicit configuration (Smtp, SendGrid, Mailgun)
    /// SaaS: SendGrid or Mailgun with API keys
    /// </summary>
    public static IServiceCollection AddEmailSender(this IServiceCollection services)
    {
        services.AddOptions<EmailOptions>()
            .BindConfiguration(EmailOptions.SectionName)
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "Email:BaseUrl must be configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Provider), "Email:Provider must be configured.")
            .Validate(o => EmailOptions.ValidProviders.Contains(o.Provider, StringComparer.OrdinalIgnoreCase),
                $"Email:Provider must be one of: {string.Join(", ", EmailOptions.ValidProviders)}")
            .Validate(o => !IsPlaceholder(o), "Email configuration contains placeholder/example values.")
            .ValidateOnStart();

        services.AddScoped<IIdentityEmailSender>(sp =>
        {
            var env = sp.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>();
            var options = sp.GetRequiredService<IOptions<EmailOptions>>().Value;
            var loggerFactory = sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>();

            return (env.IsDevelopment(), options.Provider.ToLowerInvariant()) switch
            {
                // Development: always use Console (logs tokens for easy testing)
                (true, _) => new ConsoleEmailSender(
                    loggerFactory.CreateLogger<ConsoleEmailSender>(),
                    sp.GetRequiredService<DevEmailInbox>()),

                // Production: require explicit provider configuration
                (false, "smtp") => CreateSmtpSender(sp),
                (false, "sendgrid") => CreateSendGridSender(sp),
                (false, "mailgun") => CreateMailgunSender(sp),

                // Production with no/invalid provider: fail fast with clear message
                (false, _) => throw new InvalidOperationException(
                    $"Production environment requires a valid Email:Provider. " +
                    $"Configure 'Smtp', 'SendGrid', or 'Mailgun' in appsettings.Production.json. " +
                    $"Current value: '{options.Provider}'")
            };
        });

        return services;
    }

    private static IIdentityEmailSender CreateSmtpSender(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<IOptions<EmailOptions>>().Value;
        if (string.IsNullOrWhiteSpace(options.Smtp.Host) ||
            string.IsNullOrWhiteSpace(options.Smtp.FromEmail) ||
            string.IsNullOrWhiteSpace(options.Smtp.Username) ||
            string.IsNullOrWhiteSpace(options.Smtp.Password))
        {
            throw new InvalidOperationException(
                "Email:Smtp requires Host, FromEmail, Username, and Password to be configured.");
        }
        return ActivatorUtilities.CreateInstance<SmtpEmailSender>(sp);
    }

    private static IIdentityEmailSender CreateSendGridSender(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<IOptions<EmailOptions>>().Value;
        if (string.IsNullOrWhiteSpace(options.SendGrid.ApiKey) ||
            string.IsNullOrWhiteSpace(options.SendGrid.FromEmail))
        {
            throw new InvalidOperationException(
                "Email:SendGrid requires ApiKey and FromEmail to be configured.");
        }
        // Register HttpClient if not already registered
        var httpClient = sp.GetService<HttpClient>() ?? new HttpClient();
        return new SendGridEmailSender(
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SendGridEmailSender>>(),
            sp.GetRequiredService<IOptions<EmailOptions>>(),
            httpClient);
    }

    private static IIdentityEmailSender CreateMailgunSender(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<IOptions<EmailOptions>>().Value;
        if (string.IsNullOrWhiteSpace(options.Mailgun.Domain) ||
            string.IsNullOrWhiteSpace(options.Mailgun.ApiKey) ||
            string.IsNullOrWhiteSpace(options.Mailgun.FromEmail))
        {
            throw new InvalidOperationException(
                "Email:Mailgun requires Domain, ApiKey, and FromEmail to be configured.");
        }
        var httpClient = sp.GetService<HttpClient>() ?? new HttpClient();
        return new MailgunEmailSender(
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<MailgunEmailSender>>(),
            sp.GetRequiredService<IOptions<EmailOptions>>(),
            httpClient);
    }

    private static bool IsPlaceholder(EmailOptions o)
    {
        var provider = o.Provider?.ToLowerInvariant() ?? string.Empty;
        return provider switch
        {
            "smtp" => IsPlaceholderValue(o.Smtp.Host) || IsPlaceholderValue(o.Smtp.FromEmail) ||
                      IsPlaceholderValue(o.Smtp.Username) || IsPlaceholderValue(o.Smtp.Password),
            "sendgrid" => IsPlaceholderValue(o.SendGrid.ApiKey) || IsPlaceholderValue(o.SendGrid.FromEmail),
            "mailgun" => IsPlaceholderValue(o.Mailgun.ApiKey) || IsPlaceholderValue(o.Mailgun.FromEmail) || IsPlaceholderValue(o.Mailgun.Domain),
            _ => false
        };
    }

    private static bool IsPlaceholderValue(string? v) =>
        !string.IsNullOrWhiteSpace(v) &&
        (v.Contains("your-", StringComparison.OrdinalIgnoreCase) ||
         v.Contains("example", StringComparison.OrdinalIgnoreCase));
}