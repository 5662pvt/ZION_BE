using Moq;
using ZIONShop.Admin.Application.Common;
using ZIONShop.Admin.Application.Features.GetDashboard;
using ZIONShop.Admin.Tests.TestSupport;
using ZIONShop.Contracts.Auth;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Orders;

namespace ZIONShop.Admin.Tests.Features;

public class GetDashboardQueryHandlerTests
{
    private static readonly FakeTimeProvider Now = new(new DateTimeOffset(2026, 10, 13, 4, 0, 0, TimeSpan.Zero));

    private static readonly IReadOnlyDictionary<string, int> ZeroStatusCounts = new Dictionary<string, int>
    {
        ["PendingStock"] = 0, ["AwaitingPayment"] = 0, ["Confirmed"] = 0, ["Paid"] = 0,
        ["Shipped"] = 0, ["Completed"] = 0, ["Cancelled"] = 0
    };

    [Fact]
    public async Task Handle_Should_ReturnNullChangePercent_When_PreviousPeriodHasNoData()
    {
        var range = ReportRange.Create("2026-10-01", "2026-10-07", Now).Value;

        var order = new CompletedOrderRow(
            Guid.NewGuid(), new DateTime(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc),
            Subtotal: 100m, DiscountAmount: 10m, ShippingFee: 5m,
            PaymentMethod: "COD", CouponCode: null, DiscountTarget: null,
            Lines: Array.Empty<ReportOrderLine>());

        var statusCounts = new Dictionary<string, int>(ZeroStatusCounts) { ["Completed"] = 1, ["Cancelled"] = 2 };

        var orders = new Mock<IOrderReportReader>();
        orders.Setup(x => x.GetCompletedOrdersAsync(range.FromUtc, range.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { order });
        orders.Setup(x => x.GetCompletedOrdersAsync(range.PreviousFromUtc, range.PreviousToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CompletedOrderRow>());
        orders.Setup(x => x.GetPlacedStatusCountsAsync(range.FromUtc, range.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<string, int>)statusCounts);
        orders.Setup(x => x.GetPlacedStatusCountsAsync(range.PreviousFromUtc, range.PreviousToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ZeroStatusCounts);
        orders.Setup(x => x.CountAwaitingShipmentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(3);

        var users = new Mock<IUserStatsReader>();
        users.Setup(x => x.CountCustomersRegisteredAsync(range.FromUtc, range.ToUtc, It.IsAny<CancellationToken>())).ReturnsAsync(5);
        users.Setup(x => x.CountCustomersRegisteredAsync(range.PreviousFromUtc, range.PreviousToUtc, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var inventory = new Mock<IInventoryStatsReader>();
        inventory.Setup(x => x.CountLowStockAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var handler = new GetDashboardQueryHandler(orders.Object, users.Object, inventory.Object, Now, new FakeDistributedCacheService());
        var result = await handler.Handle(new GetDashboardQuery("2026-10-01", "2026-10-07"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var kpis = result.Value.Kpis;

        kpis.Revenue.Value.Should().Be(95m);
        kpis.Revenue.Previous.Should().Be(0m);
        kpis.Revenue.ChangePercent.Should().BeNull();
        kpis.CompletedOrders.Value.Should().Be(1);
        kpis.AverageOrderValue.Value.Should().Be(95m);
        kpis.NewCustomers.Value.Should().Be(5);
        kpis.PlacedOrders.Value.Should().Be(3);
        kpis.CancelledOrders.Value.Should().Be(2);
        kpis.DiscountTotal.Value.Should().Be(10m);
        kpis.AwaitingShipment.Should().Be(3);
        kpis.LowStockProducts.Should().Be(2);

        result.Value.PaymentMethods.Should().ContainSingle(p => p.Method == "COD" && p.Orders == 1 && p.Revenue == 95m);
        result.Value.RevenueSeries.Should().HaveCount(7);
        result.Value.RevenueSeries.Single(b => b.Bucket == "2026-10-03").Revenue.Should().Be(95m);
        result.Value.RevenueSeries.Where(b => b.Bucket != "2026-10-03").Should().OnlyContain(b => b.Revenue == 0m && b.Orders == 0);
        result.Value.StatusBreakdown.Should().HaveCount(7);
        result.Value.StatusBreakdown.Single(s => s.Status == "Cancelled").Count.Should().Be(2);
    }

    [Fact]
    public async Task Handle_Should_ComputeChangePercent_When_PreviousPeriodHasRevenue()
    {
        var range = ReportRange.Create("2026-10-01", "2026-10-07", Now).Value;

        var currentOrder = new CompletedOrderRow(
            Guid.NewGuid(), new DateTime(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc),
            150m, 0m, 0m, "COD", null, null, Array.Empty<ReportOrderLine>());
        var previousOrder = new CompletedOrderRow(
            Guid.NewGuid(), new DateTime(2026, 9, 26, 10, 0, 0, DateTimeKind.Utc),
            100m, 0m, 0m, "COD", null, null, Array.Empty<ReportOrderLine>());

        var orders = new Mock<IOrderReportReader>();
        orders.Setup(x => x.GetCompletedOrdersAsync(range.FromUtc, range.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { currentOrder });
        orders.Setup(x => x.GetCompletedOrdersAsync(range.PreviousFromUtc, range.PreviousToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { previousOrder });
        orders.Setup(x => x.GetPlacedStatusCountsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ZeroStatusCounts);
        orders.Setup(x => x.CountAwaitingShipmentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var users = new Mock<IUserStatsReader>();
        users.Setup(x => x.CountCustomersRegisteredAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var inventory = new Mock<IInventoryStatsReader>();
        inventory.Setup(x => x.CountLowStockAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var handler = new GetDashboardQueryHandler(orders.Object, users.Object, inventory.Object, Now, new FakeDistributedCacheService());
        var result = await handler.Handle(new GetDashboardQuery("2026-10-01", "2026-10-07"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Kpis.Revenue.ChangePercent.Should().Be(50.0m);
    }

    [Fact]
    public async Task Handle_Should_HitCache_When_SameRangeRequestedTwice()
    {
        var range = ReportRange.Create("2026-10-01", "2026-10-07", Now).Value;

        var orders = new Mock<IOrderReportReader>();
        orders.Setup(x => x.GetCompletedOrdersAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CompletedOrderRow>());
        orders.Setup(x => x.GetPlacedStatusCountsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ZeroStatusCounts);
        orders.Setup(x => x.CountAwaitingShipmentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var users = new Mock<IUserStatsReader>();
        users.Setup(x => x.CountCustomersRegisteredAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var inventory = new Mock<IInventoryStatsReader>();
        inventory.Setup(x => x.CountLowStockAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var cache = new FakeDistributedCacheService();
        var handler = new GetDashboardQueryHandler(orders.Object, users.Object, inventory.Object, Now, cache);

        await handler.Handle(new GetDashboardQuery("2026-10-01", "2026-10-07"), CancellationToken.None);
        await handler.Handle(new GetDashboardQuery("2026-10-01", "2026-10-07"), CancellationToken.None);

        orders.Verify(x => x.GetCompletedOrdersAsync(range.FromUtc, range.ToUtc, It.IsAny<CancellationToken>()), Times.Once);
    }
}
