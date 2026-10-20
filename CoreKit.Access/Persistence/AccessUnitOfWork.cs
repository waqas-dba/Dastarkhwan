namespace CoreKit.Access.Persistence;

internal enum SaveOutcome
{
    Saved,

    /// <summary>A unique or foreign-key rule refused the change, usually because of a race.</summary>
    ConstraintViolation,

    /// <summary>Someone else changed the same role first.</summary>
    ConcurrencyConflict
}

/// <summary>Saves everything staged by the repositories in one transaction, so a change and its audit entry go together.</summary>
internal interface IAccessUnitOfWork
{
    Task<SaveOutcome> SaveAsync(CancellationToken ct = default);
}

internal sealed class AccessUnitOfWork : IAccessUnitOfWork
{
    private readonly AccessDbContext _db;
    private readonly ILogger<AccessUnitOfWork> _logger;

    public AccessUnitOfWork(AccessDbContext db, ILogger<AccessUnitOfWork> logger)
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
            _logger.LogWarning(ex, "An access change lost a concurrency check.");
            _db.ChangeTracker.Clear();
            return SaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "An access change was refused by the database.");
            _db.ChangeTracker.Clear();
            return SaveOutcome.ConstraintViolation;
        }
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