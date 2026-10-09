using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.Outbox;
using ZIONShop.Inventory.Application.Interfaces;
using ZIONShop.Inventory.Infrastructure.Persistence;

namespace ZIONShop.Inventory.Infrastructure.Outbox;

public class InventoryOutbox : IInventoryOutbox
{
    private readonly IOutbox<InventoryDbContext> _outbox;

    public InventoryOutbox(IOutbox<InventoryDbContext> outbox) => _outbox = outbox;

    public void Add(IntegrationEvent @event) => _outbox.Add(@event);
}
