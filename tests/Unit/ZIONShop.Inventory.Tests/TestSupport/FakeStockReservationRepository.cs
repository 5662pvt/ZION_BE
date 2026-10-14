using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Enums;
using ZIONShop.Inventory.Domain.Repositories;

namespace ZIONShop.Inventory.Tests.TestSupport;

internal sealed class FakeStockReservationRepository : IStockReservationRepository
{
    public List<StockReservation> Reservations { get; } = new();

    public Task<List<StockReservation>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Reservations.Where(r => r.OrderId == orderId).ToList());

    public Task<List<StockReservation>> GetActiveExpiredAsync(DateTime asOf, int batchSize = 200, CancellationToken cancellationToken = default) =>
        Task.FromResult(Reservations
            .Where(r => r.Status == ReservationStatus.Active && r.ExpiresAt != null && r.ExpiresAt <= asOf)
            .OrderBy(r => r.ExpiresAt)
            .Take(batchSize)
            .ToList());

    public Task AddAsync(StockReservation reservation, CancellationToken cancellationToken = default)
    {
        Reservations.Add(reservation);
        return Task.CompletedTask;
    }
}
