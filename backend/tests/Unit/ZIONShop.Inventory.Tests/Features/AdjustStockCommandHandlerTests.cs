using Microsoft.EntityFrameworkCore;
using Moq;
using ZIONShop.Contracts.Products;
using ZIONShop.Inventory.Application.Features.AdjustStock;
using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Exceptions;
using ZIONShop.Inventory.Infrastructure.Persistence;
using ZIONShop.Inventory.Infrastructure.Repositories;
using ZIONShop.Inventory.Tests.TestSupport;

namespace ZIONShop.Inventory.Tests.Features;

public class AdjustStockCommandHandlerTests
{
    private static InventoryDbContext CreateDb(string name) => new(new DbContextOptionsBuilder<InventoryDbContext>().UseInMemoryDatabase(name).Options);

    [Fact]
    public async Task Handle_Should_IncreaseOnHand_When_ItemExists()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var productId = Guid.NewGuid();

        using (var seedDb = CreateDb(dbName))
        {
            await seedDb.InventoryItems.AddAsync(InventoryItem.Create(productId, "SKU-A", "Product A", 10));
            await seedDb.SaveChangesAsync();
        }

        using var db = CreateDb(dbName);
        var catalog = new Mock<IProductCatalogReader>();
        var handler = new AdjustStockCommandHandler(new InventoryItemRepository(db), new StockMovementRepository(db), db, catalog.Object, new FakeTimeProvider(DateTimeOffset.UtcNow));

        var result = await handler.Handle(new AdjustStockCommand(productId, 20, "Restock", "admin@zion.test"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.OnHand.Should().Be(30);
    }

    [Fact]
    public async Task Handle_Should_CreateItem_When_MissingButProductExistsInCatalog()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var productId = Guid.NewGuid();

        using var db = CreateDb(dbName);
        var catalog = new Mock<IProductCatalogReader>();
        catalog.Setup(c => c.GetAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductCatalogItem(productId, "SKU-NEW", "New Product", "Published", null));
        var handler = new AdjustStockCommandHandler(new InventoryItemRepository(db), new StockMovementRepository(db), db, catalog.Object, new FakeTimeProvider(DateTimeOffset.UtcNow));

        var result = await handler.Handle(new AdjustStockCommand(productId, 15, "Initial stock", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.OnHand.Should().Be(15);
        result.Value.Sku.Should().Be("SKU-NEW");
    }

    [Fact]
    public async Task Handle_Should_ReturnProductNotFound_When_ItemMissingAndCatalogHasNoProduct()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var productId = Guid.NewGuid();

        using var db = CreateDb(dbName);
        var catalog = new Mock<IProductCatalogReader>();
        catalog.Setup(c => c.GetAsync(productId, It.IsAny<CancellationToken>())).ReturnsAsync((ProductCatalogItem?)null);
        var handler = new AdjustStockCommandHandler(new InventoryItemRepository(db), new StockMovementRepository(db), db, catalog.Object, new FakeTimeProvider(DateTimeOffset.UtcNow));

        var result = await handler.Handle(new AdjustStockCommand(productId, 15, "Initial stock", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.ProductNotFound);
    }

    [Fact]
    public async Task Handle_Should_Fail_When_AdjustmentWouldGoNegative()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var productId = Guid.NewGuid();

        using (var seedDb = CreateDb(dbName))
        {
            await seedDb.InventoryItems.AddAsync(InventoryItem.Create(productId, "SKU-A", "Product A", 5));
            await seedDb.SaveChangesAsync();
        }

        using var db = CreateDb(dbName);
        var catalog = new Mock<IProductCatalogReader>();
        var handler = new AdjustStockCommandHandler(new InventoryItemRepository(db), new StockMovementRepository(db), db, catalog.Object, new FakeTimeProvider(DateTimeOffset.UtcNow));

        var result = await handler.Handle(new AdjustStockCommand(productId, -10, "Damage", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.InvalidAdjustment);
    }
}
