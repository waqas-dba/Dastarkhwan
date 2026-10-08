

namespace CoreKit.Tenant.Persistence;

internal enum SaveOutcome
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
internal interface ITenantUnitOfWork
{
    Task<SaveOutcome> SaveAsync(CancellationToken ct = default);
}

internal sealed class TenantUnitOfWork : ITenantUnitOfWork
{
    private readonly TenantDbContext _db;
    private readonly ILogger<TenantUnitOfWork> _logger;

    public TenantUnitOfWork(TenantDbContext db, ILogger<TenantUnitOfWork> logger)
    {
        _db = db;
        _logger = logger;
    }

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
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "A tenant change was refused by the database.");
            _db.ChangeTracker.Clear();
            return SaveOutcome.ConstraintViolation;
        }
    }
}

internal static class SaveOutcomeExtensions
{
    /// <summary>Null when the save worked.</summary>
    public static TenantError? ToError(this SaveOutcome outcome) => outcome switch
    {
        SaveOutcome.Saved => null,
        _ => TenantErrors.SaveConflict
    };
}