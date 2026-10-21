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

    private static Mock<IProductCatalogReader> DefaultVariantCatalog(Guid productId, string sku, string productName)
    {
        var catalog = new Mock<IProductCatalogReader>();
        var variant = new ProductVariantCatalogItem(productId, productId, sku, productName, string.Empty, true, "Published");
        catalog.Setup(c => c.GetVariantAsync(productId, It.IsAny<CancellationToken>())).ReturnsAsync(variant);
        return catalog;
    }

    [Fact]
    public async Task Handle_Should_IncreaseOnHand_When_ItemExists()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var productId = Guid.NewGuid();

        using (var seedDb = CreateDb(dbName))
        {
            await seedDb.InventoryItems.AddAsync(InventoryItem.Create(productId, productId, "SKU-A", "Product A", null, 10));
            await seedDb.SaveChangesAsync();
        }

        using var db = CreateDb(dbName);
        var catalog = DefaultVariantCatalog(productId, "SKU-A", "Product A");
        var handler = new AdjustStockCommandHandler(new InventoryItemRepository(db), new StockMovementRepository(db), db, catalog.Object, new FakeTimeProvider(DateTimeOffset.UtcNow));

        var result = await handler.Handle(new AdjustStockCommand(productId, false, 20, "Restock", "admin@zion.test"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.OnHand.Should().Be(30);
    }

    [Fact]
    public async Task Handle_Should_CreateItem_When_MissingButProductExistsInCatalog()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var productId = Guid.NewGuid();

        using var db = CreateDb(dbName);
        var catalog = DefaultVariantCatalog(productId, "SKU-NEW", "New Product");
        var handler = new AdjustStockCommandHandler(new InventoryItemRepository(db), new StockMovementRepository(db), db, catalog.Object, new FakeTimeProvider(DateTimeOffset.UtcNow));

        var result = await handler.Handle(new AdjustStockCommand(productId, false, 15, "Initial stock", null), CancellationToken.None);

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
        catalog.Setup(c => c.GetVariantAsync(productId, It.IsAny<CancellationToken>())).ReturnsAsync((ProductVariantCatalogItem?)null);
        catalog.Setup(c => c.ListVariantsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<ProductVariantCatalogItem>());
        var handler = new AdjustStockCommandHandler(new InventoryItemRepository(db), new StockMovementRepository(db), db, catalog.Object, new FakeTimeProvider(DateTimeOffset.UtcNow));

        var result = await handler.Handle(new AdjustStockCommand(productId, false, 15, "Initial stock", null), CancellationToken.None);

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
            await seedDb.InventoryItems.AddAsync(InventoryItem.Create(productId, productId, "SKU-A", "Product A", null, 5));
            await seedDb.SaveChangesAsync();
        }

        using var db = CreateDb(dbName);
        var catalog = DefaultVariantCatalog(productId, "SKU-A", "Product A");
        var handler = new AdjustStockCommandHandler(new InventoryItemRepository(db), new StockMovementRepository(db), db, catalog.Object, new FakeTimeProvider(DateTimeOffset.UtcNow));

        var result = await handler.Handle(new AdjustStockCommand(productId, false, -10, "Damage", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.InvalidAdjustment);
    }

    [Fact]
    public async Task Handle_Should_AdjustByVariantId_When_IsVariantIdTrue_WithoutCatalogResolution()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();

        using (var seedDb = CreateDb(dbName))
        {
            await seedDb.InventoryItems.AddAsync(InventoryItem.Create(productId, variantId, "TEE-ZB-RED-M", "Ao Thun", "Đỏ / M", 10));
            await seedDb.SaveChangesAsync();
        }

        using var db = CreateDb(dbName);
        var catalog = new Mock<IProductCatalogReader>(MockBehavior.Strict); // must never be consulted when IsVariantId is true
        var handler = new AdjustStockCommandHandler(new InventoryItemRepository(db), new StockMovementRepository(db), db, catalog.Object, new FakeTimeProvider(DateTimeOffset.UtcNow));

        var result = await handler.Handle(new AdjustStockCommand(variantId, true, 5, "Restock", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.OnHand.Should().Be(15);
        result.Value.VariantId.Should().Be(variantId);
        result.Value.ProductId.Should().Be(productId);
    }

    [Fact]
    public async Task Handle_Should_ReturnVariantRequired_When_ProductIdHasMultipleVariants()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var productId = Guid.NewGuid();
        var variantA = Guid.NewGuid();
        var variantB = Guid.NewGuid();

        using var db = CreateDb(dbName);
        var catalog = new Mock<IProductCatalogReader>();
        catalog.Setup(c => c.GetVariantAsync(productId, It.IsAny<CancellationToken>())).ReturnsAsync((ProductVariantCatalogItem?)null);
        catalog.Setup(c => c.ListVariantsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new ProductVariantCatalogItem(variantA, productId, "TEE-ZB-RED-M", "Ao Thun", "Đỏ / M", true, "Published"),
            new ProductVariantCatalogItem(variantB, productId, "TEE-ZB-BLUE-M", "Ao Thun", "Xanh / M", true, "Published")
        });
        var handler = new AdjustStockCommandHandler(new InventoryItemRepository(db), new StockMovementRepository(db), db, catalog.Object, new FakeTimeProvider(DateTimeOffset.UtcNow));

        var result = await handler.Handle(new AdjustStockCommand(productId, false, 10, "Restock", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.VariantRequired);
    }
}
