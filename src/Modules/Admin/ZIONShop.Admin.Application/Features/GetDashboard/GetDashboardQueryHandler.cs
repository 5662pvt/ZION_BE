using System.Globalization;
using MediatR;
using ZIONShop.Admin.Application.Caching;
using ZIONShop.Admin.Application.Common;
using ZIONShop.Admin.Application.DTOs;
using ZIONShop.Caching;
using ZIONShop.Contracts.Auth;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Orders;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Admin.Application.Features.GetDashboard;

public class GetDashboardQueryHandler : IRequestHandler<GetDashboardQuery, Result<DashboardReportDto>>
{
    private static readonly string[] StatusOrder =
    {
        "PendingStock", "AwaitingPayment", "Confirmed", "Paid", "Shipped", "Completed", "Cancelled"
    };

    private readonly IOrderReportReader _orders;
    private readonly IUserStatsReader _users;
    private readonly IInventoryStatsReader _inventory;
    private readonly TimeProvider _timeProvider;
    private readonly IDistributedCacheService _cache;

    public GetDashboardQueryHandler(
        IOrderReportReader orders,
        IUserStatsReader users,
        IInventoryStatsReader inventory,
        TimeProvider timeProvider,
        IDistributedCacheService cache)
    {
        _orders = orders;
        _users = users;
        _inventory = inventory;
        _timeProvider = timeProvider;
        _cache = cache;
    }

    public async Task<Result<DashboardReportDto>> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
    {
        var rangeResult = ReportRange.Create(request.From, request.To, _timeProvider);
        if (rangeResult.IsFailure) return Result.Failure<DashboardReportDto>(rangeResult.Error);
        var range = rangeResult.Value;

        var dto = await _cache.GetOrCreateAsync(
            ReportCacheKeys.Dashboard(range.From, range.To),
            async ct => (DashboardReportDto?)await BuildAsync(range, ct),
            TimeSpan.FromSeconds(60),
            cancellationToken);

        return Result.Success(dto!);
    }

    private async Task<DashboardReportDto> BuildAsync(ReportRange range, CancellationToken cancellationToken)
    {
        var current = await _orders.GetCompletedOrdersAsync(range.FromUtc, range.ToUtc, cancellationToken);
        var previous = await _orders.GetCompletedOrdersAsync(range.PreviousFromUtc, range.PreviousToUtc, cancellationToken);
        var placedCounts = await _orders.GetPlacedStatusCountsAsync(range.FromUtc, range.ToUtc, cancellationToken);
        var previousPlacedCounts = await _orders.GetPlacedStatusCountsAsync(range.PreviousFromUtc, range.PreviousToUtc, cancellationToken);
        var newCustomers = await _users.CountCustomersRegisteredAsync(range.FromUtc, range.ToUtc, cancellationToken);
        var previousNewCustomers = await _users.CountCustomersRegisteredAsync(range.PreviousFromUtc, range.PreviousToUtc, cancellationToken);
        var awaitingShipment = await _orders.CountAwaitingShipmentAsync(cancellationToken);
        var lowStockProducts = await _inventory.CountLowStockAsync(cancellationToken);

        decimal OrderTotal(CompletedOrderRow o) => o.Subtotal - o.DiscountAmount - o.PointsDiscount + o.ShippingFee;

        var revenue = current.Sum(OrderTotal);
        var previousRevenue = previous.Sum(OrderTotal);
        var completedCount = current.Count;
        var previousCompletedCount = previous.Count;
        var aov = completedCount > 0 ? revenue / completedCount : 0m;
        var previousAov = previousCompletedCount > 0 ? previousRevenue / previousCompletedCount : 0m;
        var discountTotal = current.Sum(o => o.DiscountAmount);
        var previousDiscountTotal = previous.Sum(o => o.DiscountAmount);
        var placedOrders = placedCounts.Values.Sum();
        var previousPlacedOrders = previousPlacedCounts.Values.Sum();
        var cancelledOrders = placedCounts.GetValueOrDefault("Cancelled");
        var previousCancelledOrders = previousPlacedCounts.GetValueOrDefault("Cancelled");

        var kpis = new DashboardKpisDto(
            new KpiValueDto(revenue, previousRevenue, KpiMath.ChangePercent(revenue, previousRevenue)),
            new KpiCountDto(completedCount, previousCompletedCount, KpiMath.ChangePercent(completedCount, previousCompletedCount)),
            new KpiValueDto(aov, previousAov, KpiMath.ChangePercent(aov, previousAov)),
            new KpiCountDto(newCustomers, previousNewCustomers, KpiMath.ChangePercent(newCustomers, previousNewCustomers)),
            new KpiCountDto(placedOrders, previousPlacedOrders, KpiMath.ChangePercent(placedOrders, previousPlacedOrders)),
            new KpiCountDto(cancelledOrders, previousCancelledOrders, KpiMath.ChangePercent(cancelledOrders, previousCancelledOrders)),
            new KpiValueDto(discountTotal, previousDiscountTotal, KpiMath.ChangePercent(discountTotal, previousDiscountTotal)),
            awaitingShipment,
            lowStockProducts);

        var revenueSeries = range.Buckets
            .Select(b =>
            {
                var inBucket = current.Where(o => o.CompletedAtUtc >= b.StartUtc && o.CompletedAtUtc < b.EndUtcExclusive).ToList();
                return new RevenueBucketDto(b.Label, inBucket.Sum(OrderTotal), inBucket.Count);
            })
            .ToList();

        var statusBreakdown = StatusOrder
            .Where(placedCounts.ContainsKey)
            .Select(s => new StatusBreakdownDto(s, placedCounts[s]))
            .Concat(placedCounts.Keys.Except(StatusOrder).OrderBy(s => s, StringComparer.Ordinal).Select(s => new StatusBreakdownDto(s, placedCounts[s])))
            .ToList();

        var paymentMethods = current
            .GroupBy(o => o.PaymentMethod)
            .Select(g => new PaymentMethodDto(g.Key, g.Count(), g.Sum(OrderTotal)))
            .OrderByDescending(p => p.Revenue)
            .ToList();

        var dto = new DashboardReportDto(
            range.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            range.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            range.Granularity == ReportGranularity.Day ? "day" : "month",
            kpis,
            revenueSeries,
            statusBreakdown,
            paymentMethods);

        return dto;
    }
}
