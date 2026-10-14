using ZIONShop.Contracts.Orders;

namespace ZIONShop.Notifications.Tests.Fakes;

public class FakeOrderDetailsReader : IOrderDetailsReader
{
    private readonly Dictionary<Guid, OrderDetails> _orders = new();

    public FakeOrderDetailsReader With(OrderDetails details)
    {
        _orders[details.OrderId] = details;
        return this;
    }

    public Task<OrderDetails?> GetAsync(Guid orderId, CancellationToken cancellationToken = default)
        => Task.FromResult(_orders.TryGetValue(orderId, out var order) ? order : null);
}
