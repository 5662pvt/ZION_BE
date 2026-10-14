namespace ZIONShop.Inventory.Application.Interfaces;

public interface IInventoryUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins an explicit transaction that subsequent repository calls (including the atomic
    /// <c>TryReserveAsync</c> conditional UPDATE, which bypasses the change tracker) and
    /// <see cref="SaveChangesAsync"/> enlist in automatically.
    /// </summary>
    Task<IInventoryTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
