using Microsoft.EntityFrameworkCore;
using ZIONShop.Inventory.Application.Features.GetAvailabilityBatch;
using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Infrastructure.Persistence;
using ZIONShop.Inventory.Infrastructure.Repositories;

namespace ZIONShop.Inventory.Tests.Features;

public class GetAvailabilityBatchQueryHandlerTests
{
    private static async Task<(InventoryItemRepository items, Guid seededProductId)> SeedAsync()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new InventoryDbContext(options);

        var productId = Guid.NewGuid();
        await db.InventoryItems.AddAsync(InventoryItem.Create(productId, "ZT-BATCH-ABC123", "Batch Widget", 10));
        await db.SaveChangesAsync();

        return (new InventoryItemRepository(db), productId);
    }

    [Fact]
    public async Task Handle_Should_ReturnAvailabilityForSeededProduct_And_FalseInStockForUnknownProduct()
    {
        var (items, seededProductId) = await SeedAsync();
        var unknownProductId = Guid.NewGuid();
        var handler = new GetAvailabilityBatchQueryHandler(items);

        var result = await handler.Handle(new GetAvailabilityBatchQuery(new[] { seededProductId, unknownProductId }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        var seeded = result.Value.Single(a => a.ProductId == seededProductId);
        seeded.Available.Should().Be(10);
        seeded.InStock.Should().BeTrue();

        var unknown = result.Value.Single(a => a.ProductId == unknownProductId);
        unknown.Available.Should().Be(0);
        unknown.InStock.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_Dedupe_When_SameProductIdRequestedTwice()
    {
        var (items, seededProductId) = await SeedAsync();
        var handler = new GetAvailabilityBatchQueryHandler(items);

        var result = await handler.Handle(new GetAvailabilityBatchQuery(new[] { seededProductId, seededProductId }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_Should_ReturnEmpty_When_NoProductIdsRequested()
    {
        var (items, _) = await SeedAsync();
        var handler = new GetAvailabilityBatchQueryHandler(items);

        var result = await handler.Handle(new GetAvailabilityBatchQuery(Array.Empty<Guid>()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
