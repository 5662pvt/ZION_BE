using ZIONShop.EventBus.Abstractions;
using ZIONShop.Reviews.Application.Interfaces;

namespace ZIONShop.Reviews.Tests.Fakes;

public class FakeReviewsUnitOfWork : IReviewsUnitOfWork
{
    public List<IntegrationEvent> OutboxEvents { get; } = new();

    public void AddOutboxEvent(IntegrationEvent @event) => OutboxEvents.Add(@event);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
}
