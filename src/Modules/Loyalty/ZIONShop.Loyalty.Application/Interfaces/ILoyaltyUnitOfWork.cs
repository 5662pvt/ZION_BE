using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Loyalty.Application.Interfaces;

public interface ILoyaltyUnitOfWork
{
    /// <summary>Queues an outbox event; persisted atomically with the rest of the unit of work on <see cref="SaveChangesAsync"/>.</summary>
    void AddOutboxEvent(IntegrationEvent @event);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Detaches every tracked entity except the inbox row written by
    /// <c>IdempotentIntegrationEventHandler</c> for the current delivery (if any), so a retry after a
    /// concurrency/unique-index conflict re-fetches fresh state instead of re-applying mutations on
    /// top of now-stale in-memory entities, while still letting that inbox row be persisted once the
    /// retried attempt succeeds. This also discards any outbox event queued by the failed attempt, so a
    /// successful retry doesn't queue it twice.</summary>
    void ResetBusinessChanges();
}
