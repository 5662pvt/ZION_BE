using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Repositories;

namespace ZIONShop.Inventory.Tests.TestSupport;

internal sealed class FakeStockMovementRepository : IStockMovementRepository
{
    public List<StockMovement> Movements { get; } = new();

    public Task AddAsync(StockMovement movement, CancellationToken cancellationToken = default)
    {
        Movements.Add(movement);
        return Task.CompletedTask;
    }

    public IQueryable<StockMovement> Query() => Movements.AsQueryable();
}
