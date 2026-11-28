namespace CoreKit.Access.Persistence;

public enum SaveOutcome
{
    Saved,

    /// <summary>A unique or foreign-key rule refused the change, usually because of a race.</summary>
    ConstraintViolation,

    /// <summary>Someone else changed the same role first.</summary>
    ConcurrencyConflict
}

/// <summary>Saves everything staged by the repositories in one transaction, so a change and its audit entry go together.</summary>
public interface IAccessUnitOfWork
{
    Task<SaveOutcome> SaveAsync(CancellationToken ct = default);

    /// <summary>
    /// Runs <paramref name="work"/> inside a transaction while holding a PostgreSQL
    /// transaction-scoped advisory lock for <paramref name="lockKey"/>, then saves.
    /// Serializes concurrent logical operations on the same key.
    /// </summary>
    Task<SaveOutcome> SaveWithAdvisoryLockAsync(
        string lockKey, Func<CancellationToken, Task> work, CancellationToken ct = default);
}

public sealed class AccessUnitOfWork : IAccessUnitOfWork
{
    private readonly AccessDbContext _db;
    private readonly ILogger<AccessUnitOfWork> _logger;

    public AccessUnitOfWork(AccessDbContext db, ILogger<AccessUnitOfWork> logger)
    {
        _db = db;
        _logger = logger;
    }

    private static bool IsConflictSqlState(DbUpdateException ex)
        => ex.InnerException is Npgsql.PostgresException { SqlState: { } s }
           && (s.StartsWith("23", StringComparison.Ordinal) || s == "40001" || s == "40P01");

    public async Task<SaveOutcome> SaveAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return SaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "An access change lost a concurrency check.");
            _db.ChangeTracker.Clear();
            return SaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException ex) when (IsConflictSqlState(ex))
        {
            _logger.LogError(ex, "An access change was refused by the database.");
            _db.ChangeTracker.Clear();
            return SaveOutcome.ConstraintViolation;
        }
    }

    public async Task<SaveOutcome> SaveWithAdvisoryLockAsync(
        string lockKey, Func<CancellationToken, Task> work, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockKey);
        ArgumentNullException.ThrowIfNull(work);

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                // hashtextextended returns bigint -> matches pg_advisory_xact_lock(bigint)
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", ct);
                await work(ct);
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return SaveOutcome.Saved;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "An access change lost a concurrency check.");
                _db.ChangeTracker.Clear();
                return SaveOutcome.ConcurrencyConflict;
            }
            catch (DbUpdateException ex) when (IsConflictSqlState(ex))
            {
                _logger.LogError(ex, "An access change was refused by the database.");
                _db.ChangeTracker.Clear();
                return SaveOutcome.ConstraintViolation;
            }
        });
    }
}

internal static class SaveOutcomeExtensions
{
    /// <summary>Null when the save worked.</summary>
    public static AccessError? ToError(this SaveOutcome outcome) => outcome switch
    {
        SaveOutcome.Saved => null,
        _ => AccessErrors.SaveConflict
    };
}