using ZIONShop.Contracts.Inventory;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Payments.Application.Interfaces;
using ZIONShop.Payments.Domain.Repositories;

namespace ZIONShop.Payments.Application.IntegrationEvents;

/// <summary>No-op when the projection doesn't exist (COD order) — nothing for Payments to react to.</summary>
public class InventoryReservedIntegrationEventHandler : IIntegrationEventHandler<InventoryReservedIntegrationEvent>
{
    private readonly IPayableOrderRepository _payableOrders;
    private readonly IPaymentsUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;

    public InventoryReservedIntegrationEventHandler(IPayableOrderRepository payableOrders, IPaymentsUnitOfWork uow, TimeProvider timeProvider)
    {
        _payableOrders = payableOrders;
        _uow = uow;
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(InventoryReservedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var payableOrder = await _payableOrders.GetByOrderIdAsync(@event.OrderId, cancellationToken);
        if (payableOrder is null) return;

        var result = payableOrder.MarkPayable(_timeProvider.GetUtcNow().UtcDateTime);
        if (result.IsFailure) return; // not AwaitingStock anymore -> already processed, idempotent no-op

        await _uow.SaveChangesAsync(cancellationToken);
    }
}
