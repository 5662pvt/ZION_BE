using ZIONShop.EventBus.Abstractions;
using ZIONShop.Products.Application.Interfaces;

namespace ZIONShop.Products.Tests.TestSupport;

internal sealed class FakeProductsUnitOfWork : IProductsUnitOfWork
{
    public List<IntegrationEvent> OutboxEvents { get; } = new();

    public void AddOutboxEvent(IntegrationEvent @event) => OutboxEvents.Add(@event);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
}
