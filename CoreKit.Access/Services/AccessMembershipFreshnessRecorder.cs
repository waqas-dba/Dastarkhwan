using CoreKit.Access.Settings;
using Microsoft.Extensions.Hosting;

namespace CoreKit.Access.Services;

/// <summary>
/// Writes the freshness heartbeat and manages the per-tenant quarantine list. The heartbeat is what bounds
/// how stale a positive projection answer may be; the quarantine list is the manual safety valve that denies
/// a tenant whose stream is known to be stuck.
/// </summary>
internal interface IAccessMembershipFreshnessRecorder
{
    /// <summary>Records that Tenant is alive now. The watermark never moves backwards.</summary>
    Task HeartbeatAsync(CancellationToken ct = default);

    /// <summary>Denies projection lookups for one tenant until it is cleared.</summary>
    Task QuarantineAsync(Guid tenantId, CancellationToken ct = default);

    Task ClearQuarantineAsync(Guid tenantId, CancellationToken ct = default);
}

internal sealed class AccessMembershipFreshnessRecorder : IAccessMembershipFreshnessRecorder
{
    private const int MaxAttempts = 3;

    private readonly IAccessMembershipFreshnessRepository _freshness;
    private readonly IAccessUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;
    private readonly ILogger<AccessMembershipFreshnessRecorder> _logger;

    public AccessMembershipFreshnessRecorder(
        IAccessMembershipFreshnessRepository freshness,
        IAccessUnitOfWork unitOfWork,
        TimeProvider time,
        ILogger<AccessMembershipFreshnessRecorder> logger)
    {
        _freshness = freshness;
        _unitOfWork = unitOfWork;
        _time = time;
        _logger = logger;
    }

    public async Task HeartbeatAsync(CancellationToken ct = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var existing = await _freshness.FindWatermarkAsync(ct);

            if (existing is null)
            {
                _freshness.AddWatermark(new AccessMembershipWatermark
                {
                    Id = Guid.Empty,
                    AsOf = now,
                    ReceivedAt = now
                });
            }
            else if (existing.AsOf >= now)
            {
                // A heartbeat from a clock that runs ahead, or a duplicate. Never move the watermark back.
                return;
            }
            else
            {
                existing.AsOf = now;
                existing.ReceivedAt = now;
            }

            if (await _unitOfWork.SaveAsync(ct) == SaveOutcome.Saved)
                return;
        }

        _logger.LogWarning("A freshness heartbeat was lost to concurrent writers.");
    }

    public async Task QuarantineAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || await _freshness.IsQuarantinedAsync(tenantId, ct))
            return;

        _freshness.AddQuarantine(new AccessQuarantinedTenant
        {
            TenantId = tenantId,
            Since = _time.GetUtcNow().UtcDateTime
        });

        if (await _unitOfWork.SaveAsync(ct) != SaveOutcome.Saved)
            _logger.LogWarning("A quarantine for tenant {TenantId} was lost to a concurrent write.", tenantId);
    }

    public async Task ClearQuarantineAsync(Guid tenantId, CancellationToken ct = default)
    {
        var existing = await _freshness.FindQuarantineAsync(tenantId, ct);
        if (existing is null)
            return;

        _freshness.RemoveQuarantine(existing);

        if (await _unitOfWork.SaveAsync(ct) != SaveOutcome.Saved)
            _logger.LogWarning("Clearing the quarantine for tenant {TenantId} was lost to a concurrent write.", tenantId);
    }
}

/// <summary>
/// Writes the freshness heartbeat while the projection is selected. Without it the watermark would age out and
/// every projection lookup would fail closed, so the cutover to Projection mode would silently deny everyone.
/// </summary>
internal sealed class AccessMembershipFreshnessHeartbeatService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<AccessOptions> _options;
    private readonly ILogger<AccessMembershipFreshnessHeartbeatService> _logger;

    public AccessMembershipFreshnessHeartbeatService(
        IServiceScopeFactory scopes,
        IOptions<AccessOptions> options,
        ILogger<AccessMembershipFreshnessHeartbeatService> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The heartbeat only matters in Projection mode; the legacy adapter does not read the watermark.
        if (_options.Value.MembershipSource != AccessMembershipSource.Projection)
            return;

        var interval = TimeSpan.FromSeconds(Math.Max(_options.Value.MembershipHeartbeatSeconds, 1));

        await TickAsync(stoppingToken);

        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await TickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            await scope.ServiceProvider
                .GetRequiredService<IAccessMembershipFreshnessRecorder>()
                .HeartbeatAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "A freshness heartbeat failed; the projection will fail closed until the next one.");
        }
    }
}