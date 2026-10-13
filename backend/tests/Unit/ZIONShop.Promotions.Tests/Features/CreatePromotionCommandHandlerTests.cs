using Microsoft.EntityFrameworkCore;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.Promotions.Application.Features.CreatePromotion;
using ZIONShop.Promotions.Infrastructure.Persistence;
using ZIONShop.Promotions.Infrastructure.Repositories;
using ZIONShop.Promotions.Tests.TestSupport;

namespace ZIONShop.Promotions.Tests.Features;

public class CreatePromotionCommandHandlerTests
{
    private static (PromotionsDbContext Db, CreatePromotionCommandHandler Handler, FakeProductCatalogReader Catalog) BuildScope()
    {
        var options = new DbContextOptionsBuilder<PromotionsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new PromotionsDbContext(options);
        var catalog = new FakeProductCatalogReader();
        var handler = new CreatePromotionCommandHandler(new PromotionRepository(db), db, TimeProvider.System, catalog);
        return (db, handler, catalog);
    }

    [Fact]
    public async Task Handle_Should_CreateGiftPromotion_When_GiftProductExists()
    {
        var (db, handler, catalog) = BuildScope();
        var giftProductId = Guid.NewGuid();
        catalog.WithProduct(giftProductId, null);

        var cmd = new CreatePromotionCommand(
            "Gift", null, "Gift", 0m, null, 100000m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            "Order", null, null, null, new[] { new GiftItemDto(giftProductId, 1) });

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.GiftItems.Should().ContainSingle(g => g.ProductId == giftProductId);
        (await db.Promotions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_GiftProductDoesNotExist()
    {
        var (db, handler, _) = BuildScope();
        var missingProductId = Guid.NewGuid();

        var cmd = new CreatePromotionCommand(
            "Gift", null, "Gift", 0m, null, 100000m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            "Order", null, null, null, new[] { new GiftItemDto(missingProductId, 1) });

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.GiftProductNotFound");
        (await db.Promotions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_GiftTypeHasNoGiftItems()
    {
        var (_, handler, _) = BuildScope();

        var cmd = new CreatePromotionCommand(
            "Gift", null, "Gift", 0m, null, 100000m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            "Order", null, null, null, null);

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.GiftItemsRequired");
    }
}
