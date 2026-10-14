using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Promotions;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Orders.Application.Interfaces;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.Repositories;

namespace ZIONShop.Orders.Application.IntegrationEvents;

public class InventoryReservedIntegrationEventHandler : IIntegrationEventHandler<InventoryReservedIntegrationEvent>
{
    private readonly IOrderRepository _orders;
    private readonly IOrdersUnitOfWork _uow;
    private readonly IGiftPromotionEvaluator _giftPromotions;

    public InventoryReservedIntegrationEventHandler(IOrderRepository orders, IOrdersUnitOfWork uow, IGiftPromotionEvaluator giftPromotions)
    {
        _orders = orders;
        _uow = uow;
        _giftPromotions = giftPromotions;
    }

    public async Task HandleAsync(InventoryReservedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var order = await _orders.GetByIdAsync(@event.OrderId, cancellationToken);
        if (order is null) return;
        if (order.Status != OrderStatus.PendingStock) return; // already processed, idempotent no-op

        var reservedByProduct = @event.Items.ToDictionary(i => i.ProductId, i => i.Quantity);
        var expectedByProduct = order.Lines
            .GroupBy(l => l.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        var shortProductIds = expectedByProduct
            .Where(kv => !reservedByProduct.TryGetValue(kv.Key, out var reserved) || reserved < kv.Value)
            .Select(kv => kv.Key)
            .ToList();

        if (shortProductIds.Count > 0)
        {
            var dropped = order.DropGiftLines(shortProductIds);
            foreach (var promotionId in dropped.Select(l => l.GiftPromotionId!.Value).Distinct())
            {
                var promotionStillHasLines = order.Lines.Any(l => l.IsGift && l.GiftPromotionId == promotionId);
                if (!promotionStillHasLines)
                    await _giftPromotions.ReleaseGiftAsync(order.Id, promotionId, cancellationToken);
            }
        }

        var result = order.ConfirmReservation();
        if (result.IsFailure) return; // not PendingStock anymore -> already processed, idempotent no-op

        if (order.Status == OrderStatus.Confirmed)
            _uow.AddOutboxEvent(new OrderConfirmedIntegrationEvent(order.Id, order.UserId));

        _orders.Update(order);
        await _uow.SaveChangesAsync(cancellationToken);
    }
}
