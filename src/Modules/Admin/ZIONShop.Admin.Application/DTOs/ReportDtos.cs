namespace ZIONShop.Admin.Application.DTOs;

public sealed record KpiValueDto(decimal Value, decimal Previous, decimal? ChangePercent);

public sealed record KpiCountDto(int Value, int Previous, decimal? ChangePercent);

public sealed record DashboardKpisDto(
    KpiValueDto Revenue,
    KpiCountDto CompletedOrders,
    KpiValueDto AverageOrderValue,
    KpiCountDto NewCustomers,
    KpiCountDto PlacedOrders,
    KpiCountDto CancelledOrders,
    KpiValueDto DiscountTotal,
    int AwaitingShipment,
    int LowStockProducts);

public sealed record RevenueBucketDto(string Bucket, decimal Revenue, int Orders);

public sealed record StatusBreakdownDto(string Status, int Count);

public sealed record PaymentMethodDto(string Method, int Orders, decimal Revenue);

public sealed record DashboardReportDto(
    string From,
    string To,
    string Granularity,
    DashboardKpisDto Kpis,
    IReadOnlyList<RevenueBucketDto> RevenueSeries,
    IReadOnlyList<StatusBreakdownDto> StatusBreakdown,
    IReadOnlyList<PaymentMethodDto> PaymentMethods);

public sealed record TopProductDto(Guid ProductId, string ProductName, int Quantity, decimal Revenue, int Orders);

public sealed record TopCategoryDto(Guid? CategoryId, string CategoryName, int Quantity, decimal Revenue);

public sealed record PromotionPerformanceDto(Guid PromotionId, string PromotionName, string Type, int Orders, decimal DiscountTotal, int GiftQuantity);
