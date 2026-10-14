using Microsoft.EntityFrameworkCore;
using ZIONShop.Promotions.Application.Features.GenerateCoupons;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Enums;
using ZIONShop.Promotions.Infrastructure.Persistence;
using ZIONShop.Promotions.Infrastructure.Repositories;

namespace ZIONShop.Promotions.Tests.Features;

public class GenerateCouponsCommandHandlerTests
{
    private static (PromotionsDbContext Db, GenerateCouponsCommandHandler Handler) BuildScope()
    {
        var options = new DbContextOptionsBuilder<PromotionsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new PromotionsDbContext(options);
        var handler = new GenerateCouponsCommandHandler(new PromotionRepository(db), new CouponCodeRepository(db), db, TimeProvider.System);
        return (db, handler);
    }

    [Fact]
    public async Task Handle_Should_GenerateUniqueCoupons_When_PromotionIsNotGift()
    {
        var (db, handler) = BuildScope();
        var promotion = Promotion.Create(
            "Percent", null, PromotionType.PercentageOff, 10m, null, 0m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null).Value;
        db.Promotions.Add(promotion);
        await db.SaveChangesAsync();

        var result = await handler.Handle(new GenerateCouponsCommand(promotion.Id, "GEN", 3), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_PromotionIsGift()
    {
        var (db, handler) = BuildScope();
        var giftProductId = Guid.NewGuid();
        var promotion = Promotion.Create(
            "Gift", null, PromotionType.Gift, 0m, null, 100000m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            PromotionScope.Order, null, null, null, new[] { new GiftItemInput(giftProductId, 1) }).Value;
        db.Promotions.Add(promotion);
        await db.SaveChangesAsync();

        var result = await handler.Handle(new GenerateCouponsCommand(promotion.Id, "GEN", 3), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.CouponNotAllowedForGift");
    }
}
