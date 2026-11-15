using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreKit.Tenant.Outbox;

public sealed class TenantOutboxPublisherService : BackgroundService
{
    private readonly TenantOutboxDispatcher _dispatcher;
    private readonly TenantOutboxOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<TenantOutboxPublisherService> _logger;

    public TenantOutboxPublisherService(
        TenantOutboxDispatcher dispatcher,
        IOptions<TenantOutboxOptions> options,
        TimeProvider time,
        ILogger<TenantOutboxPublisherService> logger)
    {
        _dispatcher = dispatcher;
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;

            try
            {
                processed = await _dispatcher.RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The tenant outbox publisher loop failed; it will retry.");
            }

            if (processed > 0)
                continue;

            try
            {
                await Task.Delay(_options.PollInterval, _time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}