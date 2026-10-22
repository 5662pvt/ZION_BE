using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Loyalty.Application.Interfaces;

public interface ILoyaltyUnitOfWork
{
    /// <summary>Queues an outbox event; persisted atomically with the rest of the unit of work on <see cref="SaveChangesAsync"/>.</summary>
    void AddOutboxEvent(IntegrationEvent @event);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Detaches every tracked entity, so a retry after a concurrency conflict re-fetches fresh state
    /// instead of re-applying mutations on top of now-stale in-memory entities.</summary>
    void ClearChangeTracker();
}
