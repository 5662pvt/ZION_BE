using Microsoft.Extensions.Options;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Loyalty.Application.Interfaces;
using ZIONShop.Loyalty.Application.Options;
using ZIONShop.Loyalty.Domain.Enums;

namespace ZIONShop.Loyalty.Application.IntegrationEvents;

/// <summary>Earns floor((Subtotal − itemDiscount − PointsDiscount) × EarnRatePercent / 100) xu for the order,
/// via the idempotent (UserId, Order, OrderId) lot — a redelivered/replayed event never double-credits.</summary>
public class OrderCompletedIntegrationEventHandler : IIntegrationEventHandler<OrderCompletedIntegrationEvent>
{
    private readonly ILoyaltyMutationService _mutations;
    private readonly IOrderDetailsReader _orderDetailsReader;
    private readonly IOptions<LoyaltyOptions> _options;

    public OrderCompletedIntegrationEventHandler(ILoyaltyMutationService mutations, IOrderDetailsReader orderDetailsReader, IOptions<LoyaltyOptions> options)
    {
        _mutations = mutations;
        _orderDetailsReader = orderDetailsReader;
        _options = options;
    }

    public async Task HandleAsync(OrderCompletedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var order = await _orderDetailsReader.GetAsync(@event.OrderId, cancellationToken);
        if (order is null) return;

        var itemDiscount = string.Equals(order.DiscountTarget, "Items", StringComparison.OrdinalIgnoreCase) ? order.DiscountAmount : 0m;
        var eligible = Math.Max(0m, order.Subtotal - itemDiscount - order.PointsDiscount);
        var points = (int)Math.Floor(eligible * _options.Value.EarnRatePercent / 100m);
        if (points <= 0) return;

        await _mutations.EarnAsync(@event.UserId, points, PointLotSource.Order, @event.OrderId.ToString(), @event.OrderId, null, $"Hoàn tất đơn #{order.Code}", cancellationToken);
    }
}
