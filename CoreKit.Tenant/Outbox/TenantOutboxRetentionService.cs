using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreKit.Tenant.Outbox;

public sealed class TenantOutboxRetentionOptions
{
    public const string SectionName = "Tenant:Outbox:Retention";
    public bool Enabled { get; set; } = true;
    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromHours(6);
    public TimeSpan DeliveredRetention { get; set; } = TimeSpan.FromDays(30);
    public TimeSpan DeadLetteredRetention { get; set; } = TimeSpan.FromDays(90);
    public int BatchSize { get; set; } = 1000;
}

public sealed class TenantOutboxRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly TenantOutboxRetentionOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<TenantOutboxRetentionService> _logger;

    public TenantOutboxRetentionService(
        IServiceScopeFactory scopes,
        IOptions<TenantOutboxRetentionOptions> options,
        TimeProvider time,
        ILogger<TenantOutboxRetentionService> logger)
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
            _logger.LogInformation("Tenant outbox retention is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PurgeAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Tenant outbox retention pass failed."); }

            try { await Task.Delay(_options.ScanInterval, _time, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task PurgeAsync(CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreKit.Tenant.Persistence.TenantDbContext>();
        var now = _time.GetUtcNow().UtcDateTime;
        var deliveredCutoff = now - _options.DeliveredRetention;
        var deadCutoff = now - _options.DeadLetteredRetention;

        var d = await db.OutboxMessages
            .Where(m => m.Status == CoreKit.Tenant.Entities.TenantOutboxStatus.Delivered
                     && m.DeliveredAt != null && m.DeliveredAt < deliveredCutoff)
            .OrderBy(m => m.DeliveredAt).Take(_options.BatchSize)
            .ExecuteDeleteAsync(ct);

        var x = await db.OutboxMessages
            .Where(m => m.Status == CoreKit.Tenant.Entities.TenantOutboxStatus.DeadLettered
                     && m.DeadLetteredAt != null && m.DeadLetteredAt < deadCutoff)
            .OrderBy(m => m.DeadLetteredAt).Take(_options.BatchSize)
            .ExecuteDeleteAsync(ct);

        if (d > 0 || x > 0)
            _logger.LogInformation("Tenant outbox retention removed {D} delivered and {X} dead-lettered row(s).", d, x);
    }
}

public static class TenantOutboxRetentionExtensions
{
    public static IServiceCollection AddTenantOutboxRetention(
        this IServiceCollection services, Action<TenantOutboxRetentionOptions>? configure = null)
    {
        var builder = services.AddOptions<TenantOutboxRetentionOptions>();
        if (configure is not null) builder.Configure(configure);
        builder
            .Validate(o => o.ScanInterval > TimeSpan.Zero, "ScanInterval must be positive.")
            .Validate(o => o.DeliveredRetention > TimeSpan.Zero, "DeliveredRetention must be positive.")
            .Validate(o => o.DeadLetteredRetention >= o.DeliveredRetention,
                "DeadLetteredRetention >= DeliveredRetention.")
            .Validate(o => o.BatchSize >= 1, "BatchSize must be at least 1.");
        services.AddHostedService<TenantOutboxRetentionService>();
        return services;
    }
}