using ZIONShop.EventBus.Abstractions;
using ZIONShop.Orders.Application.Interfaces;

namespace ZIONShop.Orders.Tests.Fakes;

/// <summary>Simulates a SaveChanges failure so handlers can be tested for correct compensation (e.g. releasing a coupon reservation).</summary>
public class FakeFailingUnitOfWork : IOrdersUnitOfWork
{
    public void AddOutboxEvent(IntegrationEvent @event) { }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Simulated save failure");
}
