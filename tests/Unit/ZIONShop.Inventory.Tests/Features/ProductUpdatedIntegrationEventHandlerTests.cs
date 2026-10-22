using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Products;
using ZIONShop.Inventory.Application.IntegrationEventHandlers;
using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Infrastructure.Persistence;
using ZIONShop.Inventory.Tests.TestSupport;

namespace ZIONShop.Inventory.Tests.Features;

public class ProductUpdatedIntegrationEventHandlerTests
{
    private static InventoryDbContext CreateDb(string name) => new(new DbContextOptionsBuilder<InventoryDbContext>().UseInMemoryDatabase(name).Options);

    [Fact]
    public async Task HandleAsync_Should_RenameProductAndVariant_When_BothChanged()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();

        using (var seedDb = CreateDb(dbName))
        {
            await seedDb.InventoryItems.AddAsync(InventoryItem.Create(productId, variantId, "OLD-SKU", "Old Name", "Old Label", 10));
            await seedDb.SaveChangesAsync();
        }

        using var db = CreateDb(dbName);
        var catalog = new FakeProductCatalogReader().WithVariant(variantId, productId, "NEW-SKU", "New Name", "New Label");
        var handler = new ProductUpdatedIntegrationEventHandler(new InMemoryInventoryItemRepository(db), db, catalog);

        await handler.HandleAsync(new ProductUpdatedIntegrationEvent(
            productId, "New Name", new[] { new ProductVariantSnapshot(variantId, "NEW-SKU", "New Label") }));

        using var verifyDb = CreateDb(dbName);
        var item = verifyDb.InventoryItems.Single(i => i.VariantId == variantId);
        item.ProductName.Should().Be("New Name");
        item.Sku.Should().Be("NEW-SKU");
        item.VariantLabel.Should().Be("New Label");
    }

    [Fact]
    public async Task HandleAsync_Should_IgnoreVariant_When_NotInCatalog()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var otherVariantId = Guid.NewGuid();

        using (var seedDb = CreateDb(dbName))
        {
            await seedDb.InventoryItems.AddAsync(InventoryItem.Create(productId, variantId, "SKU-A", "Old Name", "Old Label", 10));
            await seedDb.SaveChangesAsync();
        }

        using var db = CreateDb(dbName);
        // Catalog only knows about a different variant of this product -- `variantId` is unknown to it (e.g. removed).
        var catalog = new FakeProductCatalogReader().WithVariant(otherVariantId, productId, "NEW-SKU", "New Name", "New Label");
        var handler = new ProductUpdatedIntegrationEventHandler(new InMemoryInventoryItemRepository(db), db, catalog);

        await handler.HandleAsync(new ProductUpdatedIntegrationEvent(
            productId, "New Name", new[] { new ProductVariantSnapshot(variantId, "NEW-SKU", "New Label") }));

        using var verifyDb = CreateDb(dbName);
        var item = verifyDb.InventoryItems.Single(i => i.VariantId == variantId);
        item.ProductName.Should().Be("Old Name"); // unknown-to-catalog variant left untouched entirely
        item.Sku.Should().Be("SKU-A");
        item.VariantLabel.Should().Be("Old Label");
    }

    [Fact]
    public async Task HandleAsync_Should_BeIdempotent_When_NothingChanged()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();

        using (var seedDb = CreateDb(dbName))
        {
            await seedDb.InventoryItems.AddAsync(InventoryItem.Create(productId, variantId, "SKU-A", "Same Name", "Same Label", 10));
            await seedDb.SaveChangesAsync();
        }

        using var db = CreateDb(dbName);
        var catalog = new FakeProductCatalogReader().WithVariant(variantId, productId, "SKU-A", "Same Name", "Same Label");
        var handler = new ProductUpdatedIntegrationEventHandler(new InMemoryInventoryItemRepository(db), db, catalog);

        await handler.HandleAsync(new ProductUpdatedIntegrationEvent(
            productId, "Same Name", new[] { new ProductVariantSnapshot(variantId, "SKU-A", "Same Label") }));
        await handler.HandleAsync(new ProductUpdatedIntegrationEvent(
            productId, "Same Name", new[] { new ProductVariantSnapshot(variantId, "SKU-A", "Same Label") }));

        using var verifyDb = CreateDb(dbName);
        var item = verifyDb.InventoryItems.Single(i => i.VariantId == variantId);
        item.ProductName.Should().Be("Same Name");
        item.Sku.Should().Be("SKU-A");
        item.VariantLabel.Should().Be("Same Label");
    }

    [Fact]
    public async Task HandleAsync_Should_NoOp_When_ProductHasNoInventoryItems()
    {
        using var db = CreateDb(Guid.NewGuid().ToString("N"));
        var catalog = new FakeProductCatalogReader();
        var handler = new ProductUpdatedIntegrationEventHandler(new InMemoryInventoryItemRepository(db), db, catalog);

        await handler.HandleAsync(new ProductUpdatedIntegrationEvent(Guid.NewGuid(), "New Name", Array.Empty<ProductVariantSnapshot>()));
        // No exception, nothing to update -- call succeeds as a no-op.
    }

    [Fact]
    public async Task HandleAsync_Should_NoOp_When_CatalogHasNoVariantsForProduct()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();

        using (var seedDb = CreateDb(dbName))
        {
            await seedDb.InventoryItems.AddAsync(InventoryItem.Create(productId, variantId, "SKU-A", "Old Name", "Old Label", 10));
            await seedDb.SaveChangesAsync();
        }

        using var db = CreateDb(dbName);
        var catalog = new FakeProductCatalogReader(); // product deleted from the catalog -- nothing authoritative to apply
        var handler = new ProductUpdatedIntegrationEventHandler(new InMemoryInventoryItemRepository(db), db, catalog);

        await handler.HandleAsync(new ProductUpdatedIntegrationEvent(
            productId, "New Name", new[] { new ProductVariantSnapshot(variantId, "NEW-SKU", "New Label") }));

        using var verifyDb = CreateDb(dbName);
        var item = verifyDb.InventoryItems.Single(i => i.VariantId == variantId);
        item.ProductName.Should().Be("Old Name");
        item.Sku.Should().Be("SKU-A");
        item.VariantLabel.Should().Be("Old Label");
    }

    [Fact]
    public async Task HandleAsync_Should_WriteCurrentCatalogName_When_StaleEventRedeliveredAfterNewer()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();

        using (var seedDb = CreateDb(dbName))
        {
            await seedDb.InventoryItems.AddAsync(InventoryItem.Create(productId, variantId, "SKU-A", "A", "Label", 10));
            await seedDb.SaveChangesAsync();
        }

        using var db = CreateDb(dbName);
        // Real sequence of events was A -> B -> A (reverted); the catalog's current state is "A".
        var catalog = new FakeProductCatalogReader().WithVariant(variantId, productId, "SKU-A", "A", "Label");
        var handler = new ProductUpdatedIntegrationEventHandler(new InMemoryInventoryItemRepository(db), db, catalog);

        // The "A -> B" message is redelivered last (out of order), carrying a stale "B" payload.
        await handler.HandleAsync(new ProductUpdatedIntegrationEvent(
            productId, "B", new[] { new ProductVariantSnapshot(variantId, "SKU-A", "Label") }));

        using var verifyDb = CreateDb(dbName);
        var item = verifyDb.InventoryItems.Single(i => i.VariantId == variantId);
        item.ProductName.Should().Be("A"); // catalog's current name wins over the stale payload's "B"
    }
}
