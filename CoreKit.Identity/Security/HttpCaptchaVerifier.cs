using Microsoft.Extensions.Logging;

namespace CoreKit.Identity.Security;

/// <summary>siteverify client (Turnstile / hCaptcha / reCAPTCHA). Fails closed on any error.</summary>
public sealed class HttpCaptchaVerifier : ICaptchaVerifier
{
    private const int MaxTokenLength = 4096;

    private readonly IHttpClientFactory _clients;
    private readonly IdentityCaptchaOptions _options;
    private readonly ILogger<HttpCaptchaVerifier> _logger;

    public HttpCaptchaVerifier(
        IHttpClientFactory clients,
        IOptions<IdentityCaptchaOptions> options,
        ILogger<HttpCaptchaVerifier> logger)
    {
        _clients = clients;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> VerifyAsync(string token, string? remoteIp, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaxTokenLength)
            return false;

        var form = new List<KeyValuePair<string, string>>
        {
            new("secret", _options.SecretKey),
            new("response", token)
        };
        if (!string.IsNullOrEmpty(remoteIp))
            form.Add(new("remoteip", remoteIp));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.VerifyTimeoutSeconds));

        try
        {
            using var response = await _clients.CreateClient()
                .PostAsync(_options.VerifyUrl, new FormUrlEncodedContent(form), timeout.Token);
            if (!response.IsSuccessStatusCode)
                return false;

            await using var body = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var json = await JsonDocument.ParseAsync(body, cancellationToken: timeout.Token);
            return json.RootElement.TryGetProperty("success", out var success)
                   && success.ValueKind == JsonValueKind.True;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Captcha verification failed (provider unreachable or bad response).");
            return false;
        }
    }
}
