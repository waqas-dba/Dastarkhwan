using Microsoft.EntityFrameworkCore.Storage;

namespace CoreKit.Identity.Repositories;

/// <summary>
/// Wraps an ambient transaction so nested repository code can "begin" and "commit" without
/// touching it. The outermost caller still owns commit / rollback.
/// </summary>
internal sealed class JoinedTransaction : IDbContextTransaction
{
    private readonly IDbContextTransaction _outer;

    public JoinedTransaction(IDbContextTransaction outer) => _outer = outer;

    public Guid TransactionId => _outer.TransactionId;
    public void Commit() { }
    public void Rollback() { }
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
