using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CoreKit.Identity.Persistence;

namespace CoreKit.Identity.Services;

/// <summary>
/// Deletes refresh / e-mail-verification tokens one day after they expired. Revoked-but-unexpired
/// refresh tokens are kept on purpose: they are what makes replay detection work.
/// </summary>
public sealed class IdentityTokenRetentionService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private static readonly TimeSpan Grace = TimeSpan.FromDays(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _time;
    private readonly ILogger<IdentityTokenRetentionService> _logger;

    public IdentityTokenRetentionService(
        IServiceScopeFactory scopes, TimeProvider time, ILogger<IdentityTokenRetentionService> logger)
    {
        _scopes = scopes;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PurgeAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Identity token retention pass failed."); }

            try { await Task.Delay(Interval, _time, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task PurgeAsync(CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var cutoff = _time.GetUtcNow().UtcDateTime - Grace;

        var refresh = await db.RefreshTokens.Where(t => t.ExpiresAt < cutoff).ExecuteDeleteAsync(ct);
        var verify = await db.EmailVerificationTokens.Where(t => t.ExpiresAt < cutoff).ExecuteDeleteAsync(ct);

        if (refresh > 0 || verify > 0)
            _logger.LogInformation(
                "Identity token retention removed {Refresh} refresh and {Verify} verification token(s).", refresh, verify);
    }
}
