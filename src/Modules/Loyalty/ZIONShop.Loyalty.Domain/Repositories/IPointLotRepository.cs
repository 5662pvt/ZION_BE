using ZIONShop.Loyalty.Domain.Entities;
using ZIONShop.Loyalty.Domain.Enums;

namespace ZIONShop.Loyalty.Domain.Repositories;

public interface IPointLotRepository
{
    Task<PointLot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PointLot?> GetByUserSourceRefAsync(Guid userId, PointLotSource source, string sourceRef, CancellationToken cancellationToken = default);

    /// <summary>Lots with Remaining &gt; 0 and not yet expired, ordered FIFO (ExpiresAt then EarnedAt).</summary>
    Task<List<PointLot>> GetActiveForUserAsync(Guid userId, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>Active lots expiring within <paramref name="within"/> of <paramref name="now"/>, for the "expiring soon" summary.</summary>
    Task<List<PointLot>> GetExpiringSoonAsync(Guid userId, DateTime now, DateTime within, CancellationToken cancellationToken = default);

    /// <summary>Batch of lots with Remaining &gt; 0 whose ExpiresAt has passed, for the expiry scan.</summary>
    Task<List<PointLot>> GetExpiredBatchAsync(DateTime now, int batchSize, CancellationToken cancellationToken = default);

    Task AddAsync(PointLot lot, CancellationToken cancellationToken = default);

    void Update(PointLot lot);
}
