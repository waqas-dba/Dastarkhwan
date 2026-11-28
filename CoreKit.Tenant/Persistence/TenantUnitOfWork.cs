

namespace CoreKit.Tenant.Persistence;

public enum SaveOutcome
{
    Saved,

    /// <summary>A unique or foreign-key rule refused the change, usually because of a race.</summary>
    ConstraintViolation,

    /// <summary>Someone else changed the same tenant first.</summary>
    ConcurrencyConflict
}

/// <summary>
/// Saves everything staged by the repositories in one transaction, so a change and its audit entry
/// are stored together or not at all.
/// </summary>
public interface ITenantUnitOfWork
{
    Task<SaveOutcome> SaveAsync(CancellationToken ct = default);
}

public sealed class TenantUnitOfWork : ITenantUnitOfWork
{
    private readonly TenantDbContext _db;
    private readonly ILogger<TenantUnitOfWork> _logger;

    public TenantUnitOfWork(TenantDbContext db, ILogger<TenantUnitOfWork> logger)
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
            _logger.LogWarning(ex, "A tenant change lost a concurrency check.");
            _db.ChangeTracker.Clear();
            return SaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException ex) when (IsConflictSqlState(ex))
        {
            _logger.LogError(ex, "A tenant change was refused by the database.");
            _db.ChangeTracker.Clear();
            return SaveOutcome.ConstraintViolation;
        }
    }
}

public static class SaveOutcomeExtensions
{
    /// <summary>Null when the save worked.</summary>
    public static TenantError? ToError(this SaveOutcome outcome) => outcome switch
    {
        SaveOutcome.Saved => null,
        _ => TenantErrors.SaveConflict
    };
}