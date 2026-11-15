using CoreKit.Tenant.Entities;
using CoreKit.Tenant.Events;
using CoreKit.Tenant.Outbox;
using CoreKit.Tenant.Persistence;
using CoreKit.Tenant.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CoreKit.Tenant.Tests;

public sealed class TenantOutboxDispatcherTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:;Foreign Keys=True");
    private readonly ServiceProvider _provider;
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero));
    private readonly ScriptedTransport _transport = new();
    private readonly TenantOutboxOptions _options = new()
    {
        BatchSize = 10,
        LeaseDuration = TimeSpan.FromMinutes(2),
        DeliveryTimeout = TimeSpan.FromSeconds(30),
        MaxAttempts = 3,
        BaseBackoff = TimeSpan.FromSeconds(10),
        MaxBackoff = TimeSpan.FromMinutes(5)
    };

    public TenantOutboxDispatcherTests()
    {
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TenantDbContext>(o => o.UseSqlite(_connection));
        services.AddScoped<ITenantOutboxTransport>(_ => _transport);
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    private TenantOutboxDispatcher Dispatcher() => new(
        _provider.GetRequiredService<IServiceScopeFactory>(),
        Options.Create(_options),
        _time,
        NullLogger<TenantOutboxDispatcher>.Instance);

    private void Seed(Guid tenantId, long sequence, Action<TenantOutboxMessage>? tweak = null)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var message = new TenantOutboxMessage
        {
            EventId = Guid.NewGuid(),
            TenantId = tenantId,
            Sequence = sequence,
            EventType = TenantOutboxEventTypes.MemberAdded,
            SchemaVersion = 1,
            PayloadJson = "{}",
            OccurredAt = now,
            CreatedAt = now.AddSeconds(sequence),
            NextAttemptAt = now
        };
        tweak?.Invoke(message);

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
        db.OutboxMessages.Add(message);
        db.SaveChanges();
    }

    private List<TenantOutboxMessage> Rows()
    {
        using var scope = _provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TenantDbContext>()
            .OutboxMessages.AsNoTracking().OrderBy(m => m.TenantId).ThenBy(m => m.Sequence).ToList();
    }

    [Fact]
    public async Task DeliversOneTenantStreamInSequenceOrder_OneEventPerRound()
    {
        var tenant = Guid.NewGuid();
        Seed(tenant, 1);
        Seed(tenant, 2);
        Seed(tenant, 3);

        Assert.Equal(1, await Dispatcher().RunOnceAsync(default));
        Assert.Equal(new[] { TenantOutboxStatus.Delivered, TenantOutboxStatus.Pending, TenantOutboxStatus.Pending },
            Rows().Select(r => r.Status).ToArray());

        await Dispatcher().RunOnceAsync(default);
        await Dispatcher().RunOnceAsync(default);

        Assert.Equal(new long[] { 1, 2, 3 }, _transport.Delivered.Select(e => e.Sequence).ToArray());
        Assert.All(Rows(), r => Assert.Equal(TenantOutboxStatus.Delivered, r.Status));
    }

    [Fact]
    public async Task RetryLater_BacksOff_ThenRedeliversAfterTheDelay()
    {
        var tenant = Guid.NewGuid();
        Seed(tenant, 1);
        _transport.Script = _ => TenantOutboxDeliveryResult.RetryLater("downstream down");

        await Dispatcher().RunOnceAsync(default);

        var row = Assert.Single(Rows());
        Assert.Equal(TenantOutboxStatus.Pending, row.Status);
        Assert.Equal(1, row.AttemptCount);
        Assert.Null(row.LeaseOwner);
        Assert.Equal("downstream down", row.LastError);
        Assert.True(row.NextAttemptAt > _time.GetUtcNow().UtcDateTime);

        Assert.Equal(0, await Dispatcher().RunOnceAsync(default));

        _time.Advance(TimeSpan.FromMinutes(1));
        _transport.Script = _ => TenantOutboxDeliveryResult.Accepted();
        Assert.Equal(1, await Dispatcher().RunOnceAsync(default));
        Assert.Equal(TenantOutboxStatus.Delivered, Assert.Single(Rows()).Status);
    }

    [Fact]
    public async Task TransportException_IsRetriedAndRecorded()
    {
        Seed(Guid.NewGuid(), 1);
        _transport.Script = _ => throw new InvalidOperationException("boom");

        await Dispatcher().RunOnceAsync(default);

        var row = Assert.Single(Rows());
        Assert.Equal(TenantOutboxStatus.Pending, row.Status);
        Assert.Contains("boom", row.LastError);
    }

    [Fact]
    public async Task RejectedEvent_IsDeadLettered_AndBlocksOnlyItsOwnTenant()
    {
        var blocked = Guid.NewGuid();
        var other = Guid.NewGuid();
        Seed(blocked, 1);
        Seed(blocked, 2);
        Seed(other, 1);
        _transport.Script = e => e.TenantId == blocked
            ? TenantOutboxDeliveryResult.Rejected("unsupported")
            : TenantOutboxDeliveryResult.Accepted();

        await Dispatcher().RunOnceAsync(default);
        await Dispatcher().RunOnceAsync(default);

        var rows = Rows();
        var blockedRows = rows.Where(r => r.TenantId == blocked).ToList();
        Assert.Equal(TenantOutboxStatus.DeadLettered, blockedRows[0].Status);
        Assert.NotNull(blockedRows[0].DeadLetteredAt);
        Assert.Equal(TenantOutboxStatus.Pending, blockedRows[1].Status);
        Assert.Equal(0, blockedRows[1].AttemptCount);
        Assert.Equal(TenantOutboxStatus.Delivered, rows.Single(r => r.TenantId == other).Status);
    }

    [Fact]
    public async Task ExhaustingMaxAttempts_DeadLetters()
    {
        Seed(Guid.NewGuid(), 1);
        _transport.Script = _ => TenantOutboxDeliveryResult.RetryLater("still down");

        for (var i = 0; i < _options.MaxAttempts; i++)
        {
            await Dispatcher().RunOnceAsync(default);
            _time.Advance(TimeSpan.FromMinutes(10));
        }

        var row = Assert.Single(Rows());
        Assert.Equal(TenantOutboxStatus.DeadLettered, row.Status);
        Assert.Equal(_options.MaxAttempts, row.AttemptCount);
    }

    [Fact]
    public async Task ALiveLease_IsRespected_AndAnExpiredLeaseIsRecovered()
    {
        var now = _time.GetUtcNow().UtcDateTime;
        Seed(Guid.NewGuid(), 1, m =>
        {
            m.LeaseOwner = "crashed-publisher";
            m.LeaseExpiresAt = now.AddMinutes(1);
            m.AttemptCount = 1;
        });

        Assert.Equal(0, await Dispatcher().RunOnceAsync(default));

        _time.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(1, await Dispatcher().RunOnceAsync(default));

        var row = Assert.Single(Rows());
        Assert.Equal(TenantOutboxStatus.Delivered, row.Status);
        Assert.Equal(2, row.AttemptCount);
    }

    [Fact]
    public async Task LostLease_CannotAcknowledge_AndTheMessageStaysPending()
    {
        Seed(Guid.NewGuid(), 1);
        _transport.Script = e =>
        {
            // Another publisher takes over the message while this delivery is in flight.
            using var scope = _provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
            db.OutboxMessages.Where(m => m.EventId == e.EventId)
                .ExecuteUpdate(s => s.SetProperty(m => m.LeaseOwner, "other-publisher"));
            return TenantOutboxDeliveryResult.Accepted();
        };

        await Dispatcher().RunOnceAsync(default);

        var row = Assert.Single(Rows());
        Assert.Equal(TenantOutboxStatus.Pending, row.Status);
        Assert.Equal("other-publisher", row.LeaseOwner);
    }

    [Fact]
    public async Task Stats_ReportBacklogAndDeadLetters()
    {
        Seed(Guid.NewGuid(), 1, m => m.CreatedAt = _time.GetUtcNow().UtcDateTime.AddMinutes(-3));
        Seed(Guid.NewGuid(), 1, m => m.Status = TenantOutboxStatus.DeadLettered);

        var stats = await Dispatcher().GetStatsAsync(default);

        Assert.Equal(1, stats.Pending);
        Assert.Equal(1, stats.DeadLettered);
        Assert.True(stats.OldestPendingAge >= TimeSpan.FromMinutes(3));
    }

    private sealed class ScriptedTransport : ITenantOutboxTransport
    {
        public List<TenantOutboxEnvelope> Delivered { get; } = new();

        public Func<TenantOutboxEnvelope, TenantOutboxDeliveryResult> Script { get; set; }
            = _ => TenantOutboxDeliveryResult.Accepted();

        public Task<TenantOutboxDeliveryResult> DeliverAsync(TenantOutboxEnvelope envelope, CancellationToken ct)
        {
            var result = Script(envelope);
            if (result.Outcome == TenantOutboxDeliveryOutcome.Accepted)
                Delivered.Add(envelope);
            return Task.FromResult(result);
        }
    }
}