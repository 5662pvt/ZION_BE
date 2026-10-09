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

        await db.InventoryItems.AddAsync(InventoryItem.Create(Guid.NewGuid(), "ZT-CHK-ABC123", "Checkout Widget", 10));
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
}
