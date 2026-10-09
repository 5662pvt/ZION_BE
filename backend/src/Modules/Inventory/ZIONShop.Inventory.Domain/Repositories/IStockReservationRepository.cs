using ZIONShop.Inventory.Domain.Entities;

namespace ZIONShop.Inventory.Domain.Repositories;

public interface IStockReservationRepository
{
    Task<List<StockReservation>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<List<StockReservation>> GetActiveExpiredAsync(DateTime asOf, int batchSize = 200, CancellationToken cancellationToken = default);
    Task AddAsync(StockReservation reservation, CancellationToken cancellationToken = default);
}
