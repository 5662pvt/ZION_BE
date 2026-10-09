using ZIONShop.EventBus.Abstractions;
using ZIONShop.Payments.Application.Interfaces;
using ZIONShop.Payments.Infrastructure.Persistence;

namespace ZIONShop.Payments.Tests.TestSupport;

/// <summary>
/// Wraps the real (InMemory) <see cref="PaymentsDbContext"/> for persistence while capturing outbox
/// events in a plain list, so handler tests can assert on the exact integration events queued without
/// round-tripping through JSON like the real outbox table would.
/// </summary>
internal sealed class FakePaymentsUnitOfWork : IPaymentsUnitOfWork
{
    private readonly PaymentsDbContext _db;
    public List<IntegrationEvent> OutboxEvents { get; } = new();

    public FakePaymentsUnitOfWork(PaymentsDbContext db) => _db = db;

    public void AddOutboxEvent(IntegrationEvent @event) => OutboxEvents.Add(@event);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => _db.SaveChangesAsync(cancellationToken);
}
