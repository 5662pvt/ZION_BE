using Microsoft.EntityFrameworkCore;
using ZIONShop.Promotions.Application.Features.AddFlashSaleItem;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Infrastructure.Persistence;
using ZIONShop.Promotions.Infrastructure.Repositories;
using ZIONShop.Promotions.Tests.Fakes;
using ZIONShop.Promotions.Tests.TestSupport;

namespace ZIONShop.Promotions.Tests.Features;

public class AddFlashSaleItemCommandHandlerTests
{
    private static (PromotionsDbContext Db, AddFlashSaleItemCommandHandler Handler, FakeProductCatalogReader Catalog, FakeProductPriceLookup Prices, Guid FlashSaleId) BuildScope()
    {
        var options = new DbContextOptionsBuilder<PromotionsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new PromotionsDbContext(options);
        var flashSale = FlashSale.Create("Flash", DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1));
        db.FlashSales.Add(flashSale);
        db.SaveChanges();

        var catalog = new FakeProductCatalogReader();
        var prices = new FakeProductPriceLookup();
        var handler = new AddFlashSaleItemCommandHandler(new FlashSaleRepository(db), new FlashSaleItemRepository(db), catalog, prices, db);
        return (db, handler, catalog, prices, flashSale.Id);
    }

    [Fact]
    public async Task Handle_Should_AddItem_When_VariantIdGivenDirectly()
    {
        var (db, handler, catalog, _, flashSaleId) = BuildScope();
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        catalog.WithVariant(variantId, productId, sku: "SKU-RED", variantLabel: "Đỏ");

        var cmd = new AddFlashSaleItemCommand(flashSaleId, null, variantId, 50000m, 10);
        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.VariantId.Should().Be(variantId);
        result.Value.ProductId.Should().Be(productId);
        result.Value.Sku.Should().Be("SKU-RED");
        result.Value.VariantLabel.Should().Be("Đỏ");
        (await db.FlashSaleItems.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_Should_ResolveSingleActiveVariant_When_OnlyProductIdGiven()
    {
        var (db, handler, catalog, prices, flashSaleId) = BuildScope();
        var productId = Guid.NewGuid();
        catalog.WithVariant(productId, productId);
        prices.Add(productId, "Widget", 100000m, variantId: productId);

        var cmd = new AddFlashSaleItemCommand(flashSaleId, productId, null, 70000m, 5);
        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.VariantId.Should().Be(productId);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_ProductIdHasNoResolvableVariant()
    {
        var (_, handler, _, _, flashSaleId) = BuildScope();
        var productId = Guid.NewGuid();
        // No priceLookup entry -> cannot resolve a single active variant.

        var cmd = new AddFlashSaleItemCommand(flashSaleId, productId, null, 70000m, 5);
        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.FlashSaleVariantRequired");
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_VariantDoesNotExist()
    {
        var (_, handler, _, _, flashSaleId) = BuildScope();

        var cmd = new AddFlashSaleItemCommand(flashSaleId, null, Guid.NewGuid(), 70000m, 5);
        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.FlashSaleProductNotFound");
    }

    [Fact]
    public async Task Handle_Should_AllowTwoDifferentVariants_Of_SameProduct_On_SameFlashSale()
    {
        var (db, handler, catalog, _, flashSaleId) = BuildScope();
        var productId = Guid.NewGuid();
        var variantRed = Guid.NewGuid();
        var variantBlue = Guid.NewGuid();
        catalog.WithVariant(variantRed, productId, sku: "SKU-RED", variantLabel: "Đỏ");
        catalog.WithVariant(variantBlue, productId, sku: "SKU-BLUE", variantLabel: "Xanh");

        var redResult = await handler.Handle(new AddFlashSaleItemCommand(flashSaleId, null, variantRed, 40000m, 5), CancellationToken.None);
        var blueResult = await handler.Handle(new AddFlashSaleItemCommand(flashSaleId, null, variantBlue, 60000m, 5), CancellationToken.None);

        redResult.IsSuccess.Should().BeTrue();
        blueResult.IsSuccess.Should().BeTrue();
        (await db.FlashSaleItems.CountAsync(i => i.FlashSaleId == flashSaleId)).Should().Be(2);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_SameVariantAddedTwice()
    {
        var (_, handler, catalog, _, flashSaleId) = BuildScope();
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        catalog.WithVariant(variantId, productId);

        await handler.Handle(new AddFlashSaleItemCommand(flashSaleId, null, variantId, 40000m, 5), CancellationToken.None);
        var result = await handler.Handle(new AddFlashSaleItemCommand(flashSaleId, null, variantId, 45000m, 5), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.FlashSaleItemProductAlreadyAdded");
    }
}
