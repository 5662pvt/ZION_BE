using Microsoft.EntityFrameworkCore;
using Moq;
using ZIONShop.Contracts.Products;
using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Infrastructure.Persistence;
using ZIONShop.Inventory.Infrastructure.Readers;

namespace ZIONShop.Inventory.Tests.Readers;

public class ProductStockSummaryReaderTests
{
    private static InventoryDbContext CreateDb(string name) => new(new DbContextOptionsBuilder<InventoryDbContext>().UseInMemoryDatabase(name).Options);

    [Fact]
    public async Task GetAvailableByProductAsync_Should_ExcludeInactiveVariant_When_StockOnlyOnDeactivatedDefaultVariant()
    {
        var productId = Guid.NewGuid();
        using var db = CreateDb(Guid.NewGuid().ToString("N"));
        await db.InventoryItems.AddAsync(InventoryItem.Create(productId, productId, "SKU-A", "Product A", null, 15));
        await db.SaveChangesAsync();

        var catalog = new Mock<IProductCatalogReader>();
        catalog.Setup(c => c.GetVariantsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ProductVariantCatalogItem(productId, productId, "SKU-A", "Product A", string.Empty, false, "Published") });

        var reader = new ProductStockSummaryReader(db, catalog.Object);
        var result = await reader.GetAvailableByProductAsync(new[] { productId }, CancellationToken.None);

        result[productId].Should().Be(0);
    }

    [Fact]
    public async Task GetAvailableByProductAsync_Should_OnlySumActiveVariants_When_ProductHasBothActiveAndInactiveVariants()
    {
        var productId = Guid.NewGuid();
        var activeVariantId = Guid.NewGuid();
        var inactiveVariantId = Guid.NewGuid();
        using var db = CreateDb(Guid.NewGuid().ToString("N"));
        await db.InventoryItems.AddAsync(InventoryItem.Create(productId, activeVariantId, "SKU-ACTIVE", "Product A", "M", 7));
        await db.InventoryItems.AddAsync(InventoryItem.Create(productId, inactiveVariantId, "SKU-DEAD", "Product A", "S", 20));
        await db.SaveChangesAsync();

        var catalog = new Mock<IProductCatalogReader>();
        catalog.Setup(c => c.GetVariantsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ProductVariantCatalogItem(activeVariantId, productId, "SKU-ACTIVE", "Product A", "M", true, "Published"),
                new ProductVariantCatalogItem(inactiveVariantId, productId, "SKU-DEAD", "Product A", "S", false, "Published")
            });

        var reader = new ProductStockSummaryReader(db, catalog.Object);
        var result = await reader.GetAvailableByProductAsync(new[] { productId }, CancellationToken.None);

        result[productId].Should().Be(7, "only the active variant's stock counts toward product availability");
    }

    [Fact]
    public async Task GetAvailableByProductAsync_Should_ReturnZero_When_ProductHasNoInventoryRows()
    {
        var productId = Guid.NewGuid();
        using var db = CreateDb(Guid.NewGuid().ToString("N"));
        var catalog = new Mock<IProductCatalogReader>(MockBehavior.Strict); // must not be consulted when there's nothing to resolve

        var reader = new ProductStockSummaryReader(db, catalog.Object);
        var result = await reader.GetAvailableByProductAsync(new[] { productId }, CancellationToken.None);

        result[productId].Should().Be(0);
    }
}
