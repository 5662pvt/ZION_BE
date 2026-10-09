using ZIONShop.EventBus.Abstractions;
using ZIONShop.Inventory.Application.Interfaces;

namespace ZIONShop.Inventory.Tests.TestSupport;

internal sealed class FakeInventoryOutbox : IInventoryOutbox
{
    private readonly List<IntegrationEvent> _events;

    public FakeInventoryOutbox(List<IntegrationEvent> events) => _events = events;

    public void Add(IntegrationEvent @event) => _events.Add(@event);
}
