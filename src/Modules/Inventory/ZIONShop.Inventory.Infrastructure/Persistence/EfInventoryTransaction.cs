using Microsoft.EntityFrameworkCore.Storage;
using ZIONShop.Inventory.Application.Interfaces;

namespace ZIONShop.Inventory.Infrastructure.Persistence;

internal sealed class EfInventoryTransaction : IInventoryTransaction
{
    private readonly IDbContextTransaction _transaction;

    public EfInventoryTransaction(IDbContextTransaction transaction) => _transaction = transaction;

    public Task CommitAsync(CancellationToken cancellationToken = default) => _transaction.CommitAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken = default) => _transaction.RollbackAsync(cancellationToken);

    public ValueTask DisposeAsync() => _transaction.DisposeAsync();
}
