using Microsoft.EntityFrameworkCore;
using ZIONShop.Inventory.Application.Features.SearchStock;
using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Infrastructure.Persistence;
using ZIONShop.Inventory.Infrastructure.Repositories;

namespace ZIONShop.Inventory.Tests.Features;

public class SearchStockQueryHandlerTests
{
    private static async Task<InventoryItemRepository> SeedAsync()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new InventoryDbContext(options);

        await db.InventoryItems.AddAsync(InventoryItem.Create(Guid.NewGuid(), Guid.NewGuid(), "ZT-CHK-ABC123", "Checkout Widget", null, 10));
        await db.SaveChangesAsync();

        return new InventoryItemRepository(db);
    }

    [Fact]
    public async Task Handle_Should_FindItem_When_KeywordCaseDiffersFromStoredSku()
    {
        var items = await SeedAsync();
        var handler = new SearchStockQueryHandler(items);

        var result = await handler.Handle(new SearchStockQuery(1, 20, "zt-chk-abc123", false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(i => i.Sku == "ZT-CHK-ABC123");
    }

    [Fact]
    public async Task Handle_Should_ReturnEmpty_When_KeywordDoesNotMatch()
    {
        var items = await SeedAsync();
        var handler = new SearchStockQueryHandler(items);

        var result = await handler.Handle(new SearchStockQuery(1, 20, "no-such-sku", false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_FindVariant_When_KeywordMatchesVariantLabel_And_OrderByProductNameThenLabel()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new InventoryDbContext(options);

        var productId = Guid.NewGuid();
        await db.InventoryItems.AddAsync(InventoryItem.Create(productId, Guid.NewGuid(), "TEE-ZB-BLUE-M", "Ao Thun Zion", "Xanh / M", 10));
        await db.InventoryItems.AddAsync(InventoryItem.Create(productId, Guid.NewGuid(), "TEE-ZB-RED-M", "Ao Thun Zion", "Đỏ / M", 10));
        await db.SaveChangesAsync();

        var handler = new SearchStockQueryHandler(new InventoryItemRepository(db));

        var result = await handler.Handle(new SearchStockQuery(1, 20, "Đỏ", false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(i => i.VariantLabel == "Đỏ / M");

        var all = await handler.Handle(new SearchStockQuery(1, 20, "Ao Thun Zion", false), CancellationToken.None);
        all.Value.Items.Select(i => i.VariantLabel).Should().Equal("Đỏ / M", "Xanh / M");
    }
}
