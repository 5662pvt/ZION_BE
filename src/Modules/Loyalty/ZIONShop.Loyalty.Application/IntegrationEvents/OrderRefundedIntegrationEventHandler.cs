using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Loyalty.Application.Interfaces;

namespace ZIONShop.Loyalty.Application.IntegrationEvents;

/// <summary>On a refunded return: (a) revokes the points earned from the order (clamped per-lot, with a
/// revocation marker so a late earn is skipped), and (b) gives back the points spent on the order, if any
/// reservation had been committed.</summary>
public class OrderRefundedIntegrationEventHandler : IIntegrationEventHandler<OrderRefundedIntegrationEvent>
{
    private readonly ILoyaltyMutationService _mutations;
    private readonly IOrderDetailsReader _orderDetailsReader;

    public OrderRefundedIntegrationEventHandler(ILoyaltyMutationService mutations, IOrderDetailsReader orderDetailsReader)
    {
        _mutations = mutations;
        _orderDetailsReader = orderDetailsReader;
    }

    public async Task HandleAsync(OrderRefundedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var order = await _orderDetailsReader.GetAsync(@event.OrderId, cancellationToken);
        var code = order?.Code ?? @event.OrderId.ToString("N")[..8];

        await _mutations.RevokeOrderEarnAsync(@event.OrderId, @event.UserId, $"Thu hồi Xu do trả hàng đơn #{code}", cancellationToken);
        await _mutations.RefundReservationAsync(@event.OrderId, $"Hoàn Xu do trả hàng đơn #{code}", cancellationToken);
    }
}
