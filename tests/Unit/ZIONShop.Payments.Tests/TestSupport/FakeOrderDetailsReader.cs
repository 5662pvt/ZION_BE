using ZIONShop.Contracts.Orders;

namespace ZIONShop.Payments.Tests.TestSupport;

/// <summary>Returns null unless a specific order is configured, mirroring "order not found" by default.</summary>
internal sealed class FakeOrderDetailsReader : IOrderDetailsReader
{
    private readonly Dictionary<Guid, OrderDetails> _orders = new();

    public FakeOrderDetailsReader With(OrderDetails details)
    {
        _orders[details.OrderId] = details;
        return this;
    }

    public Task<OrderDetails?> GetAsync(Guid orderId, CancellationToken ct = default) =>
        Task.FromResult(_orders.TryGetValue(orderId, out var details) ? details : null);

    public static OrderDetails AwaitingPayment(Guid orderId, Guid userId, decimal total = 250000m, string currency = "VND") =>
        new(orderId, userId, "ORD-TEST", "AwaitingPayment", "Online",
            total, 0m, null, null, 0m, total, currency,
            "Test Recipient", "0900000000", "1 Test Street, Ward 1, Test Province",
            null, DateTime.UtcNow, Array.Empty<OrderDetailsLine>());

    public static OrderDetails PendingStock(Guid orderId, Guid userId, decimal total = 250000m, string currency = "VND") =>
        new(orderId, userId, "ORD-TEST", "PendingStock", "Online",
            total, 0m, null, null, 0m, total, currency,
            "Test Recipient", "0900000000", "1 Test Street, Ward 1, Test Province",
            null, DateTime.UtcNow, Array.Empty<OrderDetailsLine>());
}
