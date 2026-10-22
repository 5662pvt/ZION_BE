using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Loyalty.Application.Interfaces;
using ZIONShop.Loyalty.Domain.Enums;
using ZIONShop.Loyalty.Domain.Repositories;

namespace ZIONShop.Loyalty.Application.IntegrationEvents;

/// <summary>Moves a Reserved points reservation of the order to Committed once it is confirmed. No balance
/// change: points were already deducted from the account at reserve time.</summary>
public class OrderConfirmedIntegrationEventHandler : IIntegrationEventHandler<OrderConfirmedIntegrationEvent>
{
    private readonly IPointReservationRepository _reservations;
    private readonly ILoyaltyUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;

    public OrderConfirmedIntegrationEventHandler(IPointReservationRepository reservations, ILoyaltyUnitOfWork uow, TimeProvider timeProvider)
    {
        _reservations = reservations;
        _uow = uow;
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(OrderConfirmedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var reservation = await _reservations.GetByOrderIdAsync(@event.OrderId, cancellationToken);
        if (reservation is null || reservation.Status != PointReservationStatus.Reserved) return;

        reservation.MarkCommitted(_timeProvider.GetUtcNow().UtcDateTime);
        _reservations.Update(reservation);
        await _uow.SaveChangesAsync(cancellationToken);
    }
}
