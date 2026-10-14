namespace ZIONShop.Inventory.Application.Interfaces;

public interface IInventoryUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
