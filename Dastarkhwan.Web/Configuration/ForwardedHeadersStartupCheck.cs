using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dastarkhwan.Web.Configuration;

internal sealed class ForwardedHeadersStartupCheck : IHostedService
{
    private readonly ForwardedHeadersOptions _options;
    private readonly IHostEnvironment _env;
    private readonly ILogger<ForwardedHeadersStartupCheck> _log;

    public ForwardedHeadersStartupCheck(
        IOptions<ForwardedHeadersOptions> options,
        IHostEnvironment env,
        ILogger<ForwardedHeadersStartupCheck> log)
    {
        _options = options.Value; _env = env; _log = log;
    }

    public Task StartAsync(CancellationToken ct)
    {
        var hasProxyConfig = _options.KnownProxies.Count > 0 || _options.KnownIPNetworks.Count > 0;
        if (!_env.IsDevelopment() && !hasProxyConfig)
            _log.LogWarning(
                "ForwardedHeaders has no KnownProxies/KnownIPNetworks configured. " +
                "If this app is behind a load balancer, RemoteIpAddress will be the proxy IP, " +
                "affecting rate limiting and IP-based decisions. " +
                "Configure ForwardedHeaders:KnownProxies or KnownIPNetworks.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}