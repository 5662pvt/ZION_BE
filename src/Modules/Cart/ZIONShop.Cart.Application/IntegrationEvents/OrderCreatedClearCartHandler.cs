using ZIONShop.Cart.Application.Interfaces;
using ZIONShop.Cart.Domain.Repositories;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Cart.Application.IntegrationEvents;

public class OrderCreatedClearCartHandler : IIntegrationEventHandler<OrderCreatedIntegrationEvent>
{
    private readonly ICartRepository _carts;
    private readonly ICartUnitOfWork _uow;

    public OrderCreatedClearCartHandler(ICartRepository carts, ICartUnitOfWork uow)
    {
        _carts = carts;
        _uow = uow;
    }

    public async Task HandleAsync(OrderCreatedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var cart = await _carts.GetByUserIdAsync(@event.UserId, cancellationToken);
        if (cart is null || cart.Items.Count == 0) return; // already cleared -> idempotent no-op

        cart.Clear();
        _carts.Update(cart);
        await _uow.SaveChangesAsync(cancellationToken);
    }
}
