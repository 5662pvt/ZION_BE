using Microsoft.EntityFrameworkCore.Storage;
using ZIONShop.Inventory.Application.Interfaces;

namespace ZIONShop.Inventory.Infrastructure.Persistence;

/// <summary>
/// Wraps either a real transaction (no transaction was already open on the DbContext) or a SAVEPOINT
/// nested inside one that was (e.g. opened by the IdempotentIntegrationEventHandler decorator around
/// this handler) -- see <see cref="InventoryDbContext.BeginTransactionAsync"/>. Only the owner of the
/// outer, real transaction may actually commit/rollback/dispose it; a savepoint's "commit" is a no-op
/// (the outer transaction's own commit makes it durable) and its "rollback" undoes only the work done
/// since the savepoint, leaving the outer transaction (and anything already in it, like the inbox row)
/// intact.
/// </summary>
internal sealed class EfInventoryTransaction : IInventoryTransaction
{
    private readonly IDbContextTransaction _transaction;
    private readonly string? _savepointName;

    public EfInventoryTransaction(IDbContextTransaction transaction) => _transaction = transaction;

    public EfInventoryTransaction(IDbContextTransaction transaction, string savepointName)
    {
        _transaction = transaction;
        _savepointName = savepointName;
    }

    public Task CommitAsync(CancellationToken cancellationToken = default) =>
        _savepointName is null
            ? _transaction.CommitAsync(cancellationToken)
            : Task.CompletedTask;

    public Task RollbackAsync(CancellationToken cancellationToken = default) =>
        _savepointName is null
            ? _transaction.RollbackAsync(cancellationToken)
            : _transaction.RollbackToSavepointAsync(_savepointName, cancellationToken);

    public ValueTask DisposeAsync() =>
        _savepointName is null ? _transaction.DisposeAsync() : default;
}
