using CoreKit.Identity.Outbox;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreKit.Identity.Outbox;

public sealed class IdentityOutboxPublisherService : BackgroundService
{
    private readonly IdentityOutboxDispatcher _dispatcher;
    private readonly IdentityOutboxOptions _options;
    private readonly ILogger<IdentityOutboxPublisherService> _logger;

    public IdentityOutboxPublisherService(
        IdentityOutboxDispatcher dispatcher,
        IOptions<IdentityOutboxOptions> options,
        ILogger<IdentityOutboxPublisherService> logger)
    {
        _dispatcher = dispatcher;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Identity outbox publisher started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _dispatcher.DispatchPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Identity outbox publisher loop");
            }

            try
            {
                await Task.Delay(_options.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Identity outbox publisher stopped");
    }
}