using ZIONShop.Contracts.Orders;

namespace ZIONShop.Chat.Tests.Fakes;

internal sealed class FakeOrderDetailsReader : IOrderDetailsReader
{
    private readonly Dictionary<Guid, OrderDetails> _orders = new();

    public FakeOrderDetailsReader With(OrderDetails order)
    {
        _orders[order.OrderId] = order;
        return this;
    }

    public Task<OrderDetails?> GetAsync(Guid orderId, CancellationToken ct = default) =>
        Task.FromResult(_orders.TryGetValue(orderId, out var order) ? order : null);
}
