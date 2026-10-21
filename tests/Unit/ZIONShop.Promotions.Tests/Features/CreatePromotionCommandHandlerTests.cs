using Microsoft.EntityFrameworkCore;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.Promotions.Application.Features.CreatePromotion;
using ZIONShop.Promotions.Infrastructure.Persistence;
using ZIONShop.Promotions.Infrastructure.Repositories;
using ZIONShop.Promotions.Tests.Fakes;
using ZIONShop.Promotions.Tests.TestSupport;

namespace ZIONShop.Promotions.Tests.Features;

public class CreatePromotionCommandHandlerTests
{
    private static (PromotionsDbContext Db, CreatePromotionCommandHandler Handler, FakeProductCatalogReader Catalog, FakeProductPriceLookup Prices) BuildScope()
    {
        var options = new DbContextOptionsBuilder<PromotionsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new PromotionsDbContext(options);
        var catalog = new FakeProductCatalogReader();
        var prices = new FakeProductPriceLookup();
        var handler = new CreatePromotionCommandHandler(new PromotionRepository(db), db, TimeProvider.System, catalog, prices);
        return (db, handler, catalog, prices);
    }

    [Fact]
    public async Task Handle_Should_CreateGiftPromotion_When_GiftVariantExists()
    {
        var (db, handler, catalog, _) = BuildScope();
        var giftProductId = Guid.NewGuid();
        catalog.WithVariant(giftProductId, giftProductId);

        var cmd = new CreatePromotionCommand(
            "Gift", null, "Gift", 0m, null, 100000m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            "Order", null, null, null, new[] { new GiftItemInputDto(null, giftProductId, 1) });

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.GiftItems.Should().ContainSingle(g => g.ProductId == giftProductId && g.VariantId == giftProductId);
        (await db.Promotions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_GiftVariantDoesNotExist()
    {
        var (db, handler, _, _) = BuildScope();
        var missingVariantId = Guid.NewGuid();

        var cmd = new CreatePromotionCommand(
            "Gift", null, "Gift", 0m, null, 100000m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            "Order", null, null, null, new[] { new GiftItemInputDto(null, missingVariantId, 1) });

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.GiftProductNotFound");
        (await db.Promotions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_ProductIdOnlyHasNoSingleActiveVariant()
    {
        var (db, handler, catalog, prices) = BuildScope();
        var productId = Guid.NewGuid();
        // priceLookup has no entry for productId -> GetAsync returns null -> VariantId cannot be resolved.

        var cmd = new CreatePromotionCommand(
            "Gift", null, "Gift", 0m, null, 100000m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            "Order", null, null, null, new[] { new GiftItemInputDto(productId, null, 1) });

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.GiftVariantRequired");
        (await db.Promotions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_ResolveSingleActiveVariant_When_OnlyProductIdGiven()
    {
        var (db, handler, catalog, prices) = BuildScope();
        var productId = Guid.NewGuid();
        catalog.WithVariant(productId, productId);
        prices.Add(productId, "Cap", 50000m, variantId: productId);

        var cmd = new CreatePromotionCommand(
            "Gift", null, "Gift", 0m, null, 100000m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            "Order", null, null, null, new[] { new GiftItemInputDto(productId, null, 1) });

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.GiftItems.Should().ContainSingle(g => g.ProductId == productId && g.VariantId == productId);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_GiftVariantIsInactive()
    {
        var (db, handler, catalog, _) = BuildScope();
        var giftProductId = Guid.NewGuid();
        var giftVariantId = Guid.NewGuid();
        catalog.WithVariant(giftVariantId, giftProductId, isActive: false);

        var cmd = new CreatePromotionCommand(
            "Gift", null, "Gift", 0m, null, 100000m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            "Order", null, null, null, new[] { new GiftItemInputDto(null, giftVariantId, 1) });

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.GiftVariantInactive");
        (await db.Promotions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_GiftProductIsNotPublished()
    {
        var (db, handler, catalog, _) = BuildScope();
        var giftProductId = Guid.NewGuid();
        var giftVariantId = Guid.NewGuid();
        catalog.WithVariant(giftVariantId, giftProductId, productStatus: "Draft");

        var cmd = new CreatePromotionCommand(
            "Gift", null, "Gift", 0m, null, 100000m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            "Order", null, null, null, new[] { new GiftItemInputDto(null, giftVariantId, 1) });

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.GiftProductNotPublished");
        (await db.Promotions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_GiftTypeHasNoGiftItems()
    {
        var (_, handler, _, _) = BuildScope();

        var cmd = new CreatePromotionCommand(
            "Gift", null, "Gift", 0m, null, 100000m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            "Order", null, null, null, null);

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.GiftItemsRequired");
    }
}
