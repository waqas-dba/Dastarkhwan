using CoreKit.Identity.Outbox;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreKit.Identity.Outbox;

public sealed class IdentityOutboxPublisherService : BackgroundService
{
    private static readonly TimeSpan MaxIdlePoll = TimeSpan.FromSeconds(30);
    private readonly IServiceProvider _services;
    private readonly IdentityOutboxOptions _options;
    private readonly ILogger<IdentityOutboxPublisherService> _logger;

    public IdentityOutboxPublisherService(
        IServiceProvider services,
        IOptions<IdentityOutboxOptions> options,
        ILogger<IdentityOutboxPublisherService> logger)
    {
        _services = services;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Identity outbox publisher started");
        var idle = _options.PollInterval;

        while (!stoppingToken.IsCancellationRequested)
        {
            int processed;
            try
            {
                using var scope = _services.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IdentityOutboxDispatcher>();
                processed = await dispatcher.DispatchPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Identity outbox publisher loop");
                processed = 0;
            }

            if (processed >= _options.BatchSize)
            {
                idle = _options.PollInterval;
                continue;
            }
            idle = processed > 0
                ? _options.PollInterval
                : TimeSpan.FromTicks(Math.Min(idle.Ticks * 2, MaxIdlePoll.Ticks));

            try
            {
                await Task.Delay(idle, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Identity outbox publisher stopped");
    }
}