using Moq;
using ZIONShop.Admin.Application.Common;
using ZIONShop.Admin.Application.Features.GetTopProducts;
using ZIONShop.Admin.Tests.TestSupport;
using ZIONShop.Contracts.Orders;

namespace ZIONShop.Admin.Tests.Features;

public class GetTopProductsQueryHandlerTests
{
    private static readonly FakeTimeProvider Now = new(new DateTimeOffset(2026, 10, 13, 4, 0, 0, TimeSpan.Zero));

    private static CompletedOrderRow OrderWith(params ReportOrderLine[] lines) => new(
        Guid.NewGuid(), new DateTime(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc),
        Subtotal: 0m, DiscountAmount: 0m, ShippingFee: 0m, PaymentMethod: "COD",
        CouponCode: null, DiscountTarget: null, Lines: lines);

    private static Mock<IOrderReportReader> ReaderReturning(ReportRange range, IReadOnlyList<CompletedOrderRow> orders)
    {
        var reader = new Mock<IOrderReportReader>();
        reader.Setup(x => x.GetCompletedOrdersAsync(range.FromUtc, range.ToUtc, It.IsAny<CancellationToken>())).ReturnsAsync(orders);
        return reader;
    }

    [Fact]
    public async Task Handle_Should_ExcludeGiftLines_When_AggregatingRevenue()
    {
        var range = ReportRange.Create("2026-10-01", "2026-10-07", Now).Value;
        var productId = Guid.NewGuid();
        var giftProductId = Guid.NewGuid();

        var order = OrderWith(
            new ReportOrderLine(productId, "Real Product", 2, 100m, false, null),
            new ReportOrderLine(giftProductId, "Gift Product", 1, 50m, true, Guid.NewGuid()));

        var reader = ReaderReturning(range, new[] { order });
        var handler = new GetTopProductsQueryHandler(reader.Object, Now);

        var result = await handler.Handle(new GetTopProductsQuery("2026-10-01", "2026-10-07", 10, "revenue"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value.Single().ProductId.Should().Be(productId);
        result.Value.Single().Revenue.Should().Be(200m);
    }

    [Fact]
    public async Task Handle_Should_SortByQuantity_When_SortByIsQuantity()
    {
        var range = ReportRange.Create("2026-10-01", "2026-10-07", Now).Value;
        var highRevenueLowQty = Guid.NewGuid();
        var lowRevenueHighQty = Guid.NewGuid();

        var order = OrderWith(
            new ReportOrderLine(highRevenueLowQty, "Expensive", 1, 1000m, false, null),
            new ReportOrderLine(lowRevenueHighQty, "Cheap", 50, 1m, false, null));

        var reader = ReaderReturning(range, new[] { order });
        var handler = new GetTopProductsQueryHandler(reader.Object, Now);

        var result = await handler.Handle(new GetTopProductsQuery("2026-10-01", "2026-10-07", 10, "quantity"), CancellationToken.None);

        result.Value.First().ProductId.Should().Be(lowRevenueHighQty);
    }

    [Fact]
    public async Task Handle_Should_LimitResults_When_MoreProductsThanLimit()
    {
        var range = ReportRange.Create("2026-10-01", "2026-10-07", Now).Value;
        var lines = Enumerable.Range(0, 5)
            .Select(i => new ReportOrderLine(Guid.NewGuid(), $"Product {i}", 1, (i + 1) * 10m, false, null))
            .ToArray();
        var order = OrderWith(lines);

        var reader = ReaderReturning(range, new[] { order });
        var handler = new GetTopProductsQueryHandler(reader.Object, Now);

        var result = await handler.Handle(new GetTopProductsQuery("2026-10-01", "2026-10-07", 2, "revenue"), CancellationToken.None);

        result.Value.Should().HaveCount(2);
        result.Value.Select(p => p.Revenue).Should().BeInDescendingOrder();
    }
}
