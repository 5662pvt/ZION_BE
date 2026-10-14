using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Inventory.Application.Interfaces;
using ZIONShop.Inventory.Domain.Repositories;
using ZIONShop.Inventory.Infrastructure.Persistence;
using ZIONShop.Inventory.Infrastructure.Repositories;

namespace ZIONShop.Inventory.Tests.TestSupport;

/// <summary>
/// Builds a fresh <see cref="InventoryDbContext"/> (same InMemory database name, so data persists
/// across "scopes" the same way a real scoped DbContext would within one request) plus its repos,
/// wired through a hand-rolled <see cref="SimpleServiceScopeFactory"/>.
/// </summary>
internal sealed class InventoryScopeFactory
{
    private readonly string _dbName;
    public List<IntegrationEvent> OutboxEvents { get; } = new();

    public InventoryScopeFactory(string? dbName = null) => _dbName = dbName ?? Guid.NewGuid().ToString("N");

    public InventoryDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
        return new InventoryDbContext(options);
    }

    public SimpleServiceScopeFactory CreateScopeFactory() => new(() =>
    {
        var db = CreateDbContext();
        return new DictionaryServiceProvider()
            .Add<IInventoryItemRepository>(() => new InventoryItemRepository(db))
            .Add<IStockReservationRepository>(() => new StockReservationRepository(db))
            .Add<IStockMovementRepository>(() => new StockMovementRepository(db))
            .Add<IInventoryUnitOfWork>(() => db)
            .Add<IInventoryOutbox>(() => new FakeInventoryOutbox(OutboxEvents));
    });
}
