using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreKit.Identity.Outbox;

public sealed class IdentityOutboxRetentionOptions
{
    public const string SectionName = "Identity:Outbox:Retention";
    public bool Enabled { get; set; } = true;
    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromHours(6);
    public TimeSpan DeliveredRetention { get; set; } = TimeSpan.FromDays(30);
    public TimeSpan DeadLetteredRetention { get; set; } = TimeSpan.FromDays(90);
    public int BatchSize { get; set; } = 1000;
}

public sealed class IdentityOutboxRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IdentityOutboxRetentionOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<IdentityOutboxRetentionService> _logger;

    public IdentityOutboxRetentionService(
        IServiceScopeFactory scopes,
        IOptions<IdentityOutboxRetentionOptions> options,
        TimeProvider time,
        ILogger<IdentityOutboxRetentionService> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Identity outbox retention is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PurgeAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Identity outbox retention pass failed."); }

            try { await Task.Delay(_options.ScanInterval, _time, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task PurgeAsync(CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Persistence.IdentityDbContext>();
        var now = _time.GetUtcNow().UtcDateTime;

        var deliveredCutoff = now - _options.DeliveredRetention;
        var deadCutoff = now - _options.DeadLetteredRetention;

        var deletedDelivered = await db.OutboxMessages
            .Where(m => m.Status == Entities.IdentityOutboxStatus.Delivered
                     && m.DeliveredAt != null && m.DeliveredAt < deliveredCutoff)
            .OrderBy(m => m.DeliveredAt)
            .Take(_options.BatchSize)
            .ExecuteDeleteAsync(ct);

        var deletedDead = await db.OutboxMessages
            .Where(m => m.Status == Entities.IdentityOutboxStatus.DeadLettered
                     && m.DeadLetteredAt != null && m.DeadLetteredAt < deadCutoff)
            .OrderBy(m => m.DeadLetteredAt)
            .Take(_options.BatchSize)
            .ExecuteDeleteAsync(ct);

        if (deletedDelivered > 0 || deletedDead > 0)
            _logger.LogInformation(
                "Identity outbox retention removed {Delivered} delivered and {Dead} dead-lettered row(s).",
                deletedDelivered, deletedDead);
    }
}

public static class IdentityOutboxRetentionExtensions
{
    public static IServiceCollection AddIdentityOutboxRetention(
        this IServiceCollection services,
        Action<IdentityOutboxRetentionOptions>? configure = null)
    {
        var builder = services.AddOptions<IdentityOutboxRetentionOptions>();
        if (configure is not null) builder.Configure(configure);
        builder
            .Validate(o => o.ScanInterval > TimeSpan.Zero, "Retention ScanInterval must be positive.")
            .Validate(o => o.DeliveredRetention > TimeSpan.Zero, "DeliveredRetention must be positive.")
            .Validate(o => o.DeadLetteredRetention >= o.DeliveredRetention,
                "DeadLetteredRetention should be >= DeliveredRetention.")
            .Validate(o => o.BatchSize >= 1, "Retention BatchSize must be at least 1.");
        services.AddHostedService<IdentityOutboxRetentionService>();
        return services;
    }
}