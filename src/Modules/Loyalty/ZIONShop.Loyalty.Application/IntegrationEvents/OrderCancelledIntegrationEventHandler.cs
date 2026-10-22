using ZIONShop.Contracts.Loyalty;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Loyalty.Application.IntegrationEvents;

/// <summary>Delegates to <see cref="ILoyaltyPointsService.ReleaseAsync"/> — same idempotent release path used
/// when Orders compensates a failed checkout.</summary>
public class OrderCancelledIntegrationEventHandler : IIntegrationEventHandler<OrderCancelledIntegrationEvent>
{
    private readonly ILoyaltyPointsService _loyalty;

    public OrderCancelledIntegrationEventHandler(ILoyaltyPointsService loyalty) => _loyalty = loyalty;

    public Task HandleAsync(OrderCancelledIntegrationEvent @event, CancellationToken cancellationToken = default) =>
        _loyalty.ReleaseAsync(@event.OrderId, cancellationToken);
}
