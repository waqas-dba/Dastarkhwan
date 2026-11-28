using Microsoft.Extensions.Caching.Memory;

namespace CoreKit.Identity.Security;

/// <summary>
/// Decides whether a login attempt must carry a captcha. Counters are per process (in-memory) and keyed by a
/// hash of the e-mail - they are identical for existing and non-existing accounts, so a challenge never
/// reveals whether an account exists.
/// </summary>
public sealed class LoginRiskGate : ILoginRiskGate
{
    private sealed class Counter { public int Value; }

    private readonly IMemoryCache _cache;
    private readonly IHttpContextAccessor _http;
    private readonly ICaptchaVerifier _captcha;
    private readonly IEnumerable<ILoginRiskSignal> _signals;
    private readonly IdentityCaptchaOptions _options;

    public LoginRiskGate(
        IMemoryCache cache,
        IHttpContextAccessor http,
        ICaptchaVerifier captcha,
        IEnumerable<ILoginRiskSignal> signals,
        IOptions<IdentityCaptchaOptions> options)
    {
        _cache = cache;
        _http = http;
        _captcha = captcha;
        _signals = signals;
        _options = options.Value;
    }

    public async Task<Common.IdentityResult> CheckAsync(
        string normalizedEmail, string? captchaToken, CancellationToken ct = default)
    {
        if (_options.Mode == CaptchaMode.Off)
            return Common.IdentityResult.Success();

        var ip = RemoteIp();
        var required =
            _options.Mode == CaptchaMode.Always
            || Peek(EmailKey(normalizedEmail)) >= _options.EmailFailureThreshold
            || (ip is not null && Peek(IpKey(ip)) >= _options.IpFailureThreshold);

        if (!required)
        {
            foreach (var signal in _signals)
            {
                if (await signal.RequiresChallengeAsync(normalizedEmail, ip, ct))
                {
                    required = true;
                    break;
                }
            }
        }

        if (!required)
            return Common.IdentityResult.Success();

        if (string.IsNullOrWhiteSpace(captchaToken))
            return IdentityCaptchaErrors.Required;

        return await _captcha.VerifyAsync(captchaToken.Trim(), ip, ct)
            ? Common.IdentityResult.Success()
            : IdentityCaptchaErrors.Failed;
    }

    public void RecordFailure(string normalizedEmail)
    {
        if (_options.Mode == CaptchaMode.Off)
            return;

        Bump(EmailKey(normalizedEmail));
        if (RemoteIp() is { } ip)
            Bump(IpKey(ip));
    }

    public void RecordSuccess(string normalizedEmail)
    {
        if (_options.Mode == CaptchaMode.Off)
            return;

        _cache.Remove(EmailKey(normalizedEmail));
    }

    private int Peek(string key)
        => _cache.TryGetValue(key, out Counter? c) && c is not null ? Volatile.Read(ref c.Value) : 0;

    private void Bump(string key)
    {
        var counter = _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.WindowMinutes);
            return new Counter();
        })!;
        Interlocked.Increment(ref counter.Value);
    }

    private string? RemoteIp() => _http.HttpContext?.Connection.RemoteIpAddress?.ToString();

    private static string EmailKey(string normalizedEmail)
        => "login-risk:email:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail)))[..32];

    private static string IpKey(string ip) => "login-risk:ip:" + ip;
}
