using Moq;
using ZIONShop.Admin.Application.Common;
using ZIONShop.Admin.Application.Features.GetPromotionPerformance;
using ZIONShop.Admin.Tests.TestSupport;
using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Promotions;

namespace ZIONShop.Admin.Tests.Features;

public class GetPromotionPerformanceQueryHandlerTests
{
    private static readonly FakeTimeProvider Now = new(new DateTimeOffset(2026, 10, 13, 4, 0, 0, TimeSpan.Zero));

    private static CompletedOrderRow CouponOrder(string code, decimal discount) => new(
        Guid.NewGuid(), new DateTime(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc),
        100m, discount, 0m, "COD", code, "Order", Array.Empty<ReportOrderLine>());

    private static CompletedOrderRow GiftOrder(Guid giftPromotionId, int quantity) => new(
        Guid.NewGuid(), new DateTime(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc),
        100m, 0m, 0m, "COD", null, null,
        new[] { new ReportOrderLine(Guid.NewGuid(), "Gift Item", quantity, 10m, true, giftPromotionId) });

    [Fact]
    public async Task Handle_Should_AggregateDiscountTotal_When_OrdersUseCoupon()
    {
        var range = ReportRange.Create("2026-10-01", "2026-10-07", Now).Value;
        var promotionId = Guid.NewGuid();

        var orders = new[] { CouponOrder("SALE10", 10m), CouponOrder("SALE10", 20m) };

        var orderReader = new Mock<IOrderReportReader>();
        orderReader.Setup(x => x.GetCompletedOrdersAsync(range.FromUtc, range.ToUtc, It.IsAny<CancellationToken>())).ReturnsAsync(orders);

        var promotions = new Mock<IPromotionLookup>();
        promotions.Setup(x => x.ResolveCouponCodesAsync(It.Is<IReadOnlyCollection<string>>(c => c.Contains("SALE10")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, PromotionRef> { ["SALE10"] = new(promotionId, "10% Sale", "PercentageOff") });

        var handler = new GetPromotionPerformanceQueryHandler(orderReader.Object, promotions.Object, Now);
        var result = await handler.Handle(new GetPromotionPerformanceQuery("2026-10-01", "2026-10-07"), CancellationToken.None);

        var entry = result.Value.Should().ContainSingle().Subject;
        entry.PromotionId.Should().Be(promotionId);
        entry.Orders.Should().Be(2);
        entry.DiscountTotal.Should().Be(30m);
        entry.GiftQuantity.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_AggregateGiftQuantity_When_OrdersHaveGiftLines()
    {
        var range = ReportRange.Create("2026-10-01", "2026-10-07", Now).Value;
        var giftPromotionId = Guid.NewGuid();

        var orders = new[] { GiftOrder(giftPromotionId, 1), GiftOrder(giftPromotionId, 2) };

        var orderReader = new Mock<IOrderReportReader>();
        orderReader.Setup(x => x.GetCompletedOrdersAsync(range.FromUtc, range.ToUtc, It.IsAny<CancellationToken>())).ReturnsAsync(orders);

        var promotions = new Mock<IPromotionLookup>();
        promotions.Setup(x => x.GetByIdsAsync(It.Is<IReadOnlyCollection<Guid>>(c => c.Contains(giftPromotionId)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, PromotionRef> { [giftPromotionId] = new(giftPromotionId, "Free Gift", "Gift") });

        var handler = new GetPromotionPerformanceQueryHandler(orderReader.Object, promotions.Object, Now);
        var result = await handler.Handle(new GetPromotionPerformanceQuery("2026-10-01", "2026-10-07"), CancellationToken.None);

        var entry = result.Value.Should().ContainSingle().Subject;
        entry.PromotionId.Should().Be(giftPromotionId);
        entry.Type.Should().Be("Gift");
        entry.Orders.Should().Be(2);
        entry.GiftQuantity.Should().Be(3);
        entry.DiscountTotal.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_FallbackToUnknownType_When_CouponCodeDoesNotResolve()
    {
        var range = ReportRange.Create("2026-10-01", "2026-10-07", Now).Value;
        var orders = new[] { CouponOrder("GONE", 5m) };

        var orderReader = new Mock<IOrderReportReader>();
        orderReader.Setup(x => x.GetCompletedOrdersAsync(range.FromUtc, range.ToUtc, It.IsAny<CancellationToken>())).ReturnsAsync(orders);

        var promotions = new Mock<IPromotionLookup>();
        promotions.Setup(x => x.ResolveCouponCodesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, PromotionRef>());

        var handler = new GetPromotionPerformanceQueryHandler(orderReader.Object, promotions.Object, Now);
        var result = await handler.Handle(new GetPromotionPerformanceQuery("2026-10-01", "2026-10-07"), CancellationToken.None);

        var entry = result.Value.Should().ContainSingle().Subject;
        entry.PromotionName.Should().Be("GONE");
        entry.Type.Should().Be("Unknown");
    }
}
