using Microsoft.EntityFrameworkCore;
using Moq;
using ZIONShop.Contracts.Products;
using ZIONShop.Inventory.Application.Features.GetAvailability;
using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Exceptions;
using ZIONShop.Inventory.Infrastructure.Persistence;
using ZIONShop.Inventory.Infrastructure.Repositories;

namespace ZIONShop.Inventory.Tests.Features;

public class GetAvailabilityQueryHandlerTests
{
    private static InventoryDbContext CreateDb(string name) => new(new DbContextOptionsBuilder<InventoryDbContext>().UseInMemoryDatabase(name).Options);

    [Fact]
    public async Task Handle_Should_ReturnByVariantId_Directly_When_IsVariantIdTrue_WithoutConsultingCatalog()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();

        using (var seedDb = CreateDb(dbName))
        {
            await seedDb.InventoryItems.AddAsync(InventoryItem.Create(productId, variantId, "TEE-ZB-RED-M", "Ao Thun", "Đỏ / M", 7));
            await seedDb.SaveChangesAsync();
        }

        using var db = CreateDb(dbName);
        var catalog = new Mock<IProductCatalogReader>(MockBehavior.Strict); // must never be consulted when IsVariantId is true
        var handler = new GetAvailabilityQueryHandler(new InventoryItemRepository(db), catalog.Object);

        var result = await handler.Handle(new GetAvailabilityQuery(variantId, IsVariantId: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.VariantId.Should().Be(variantId);
        result.Value.ProductId.Should().Be(productId);
        result.Value.VariantLabel.Should().Be("Đỏ / M");
        result.Value.Available.Should().Be(7);
        result.Value.InStock.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_ResolveSingleVariant_When_IdIsProductId_WithExactlyOneVariant()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var productId = Guid.NewGuid();

        using (var seedDb = CreateDb(dbName))
        {
            await seedDb.InventoryItems.AddAsync(InventoryItem.Create(productId, productId, "SKU-A", "Product A", null, 9));
            await seedDb.SaveChangesAsync();
        }

        using var db = CreateDb(dbName);
        var catalog = new Mock<IProductCatalogReader>();
        catalog.Setup(c => c.GetVariantAsync(productId, It.IsAny<CancellationToken>())).ReturnsAsync((ProductVariantCatalogItem?)null);
        catalog.Setup(c => c.ListVariantsByProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ProductVariantCatalogItem(productId, productId, "SKU-A", "Product A", string.Empty, true, "Published") });
        var handler = new GetAvailabilityQueryHandler(new InventoryItemRepository(db), catalog.Object);

        var result = await handler.Handle(new GetAvailabilityQuery(productId, IsVariantId: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Available.Should().Be(9);
    }

    [Fact]
    public async Task Handle_Should_ReturnVariantRequired_When_IdIsProductId_WithMultipleVariants()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var productId = Guid.NewGuid();
        var variantA = Guid.NewGuid();
        var variantB = Guid.NewGuid();

        using var db = CreateDb(dbName);
        var catalog = new Mock<IProductCatalogReader>();
        catalog.Setup(c => c.GetVariantAsync(productId, It.IsAny<CancellationToken>())).ReturnsAsync((ProductVariantCatalogItem?)null);
        catalog.Setup(c => c.ListVariantsByProductAsync(productId, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new ProductVariantCatalogItem(variantA, productId, "TEE-ZB-RED-M", "Ao Thun", "Đỏ / M", true, "Published"),
            new ProductVariantCatalogItem(variantB, productId, "TEE-ZB-BLUE-M", "Ao Thun", "Xanh / M", true, "Published")
        });
        var handler = new GetAvailabilityQueryHandler(new InventoryItemRepository(db), catalog.Object);

        var result = await handler.Handle(new GetAvailabilityQuery(productId, IsVariantId: false), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.VariantRequired);
    }

    [Fact]
    public async Task Handle_Should_ReturnZeroAvailable_When_VariantIdHasNoInventoryRow()
    {
        using var db = CreateDb(Guid.NewGuid().ToString("N"));
        var variantId = Guid.NewGuid();
        var catalog = new Mock<IProductCatalogReader>(MockBehavior.Strict);
        var handler = new GetAvailabilityQueryHandler(new InventoryItemRepository(db), catalog.Object);

        var result = await handler.Handle(new GetAvailabilityQuery(variantId, IsVariantId: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Available.Should().Be(0);
        result.Value.InStock.Should().BeFalse();
    }
}
