using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Products.Application.Interfaces;

public interface IProductsUnitOfWork
{
    /// <summary>Queues an outbox event; persisted atomically with the rest of the unit of work on <see cref="SaveChangesAsync"/>.</summary>
    void AddOutboxEvent(IntegrationEvent @event);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
