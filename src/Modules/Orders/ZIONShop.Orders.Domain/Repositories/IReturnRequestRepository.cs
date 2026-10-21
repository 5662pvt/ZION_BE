using ZIONShop.Orders.Domain.Entities;

namespace ZIONShop.Orders.Domain.Repositories;

public interface IReturnRequestRepository
{
    Task<ReturnRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Latest non-deleted request for the order, or null if none exists yet.</summary>
    Task<ReturnRequest?> GetLatestByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>The order's single active (non-Rejected) request, if any.</summary>
    Task<ReturnRequest?> GetActiveByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task AddAsync(ReturnRequest request, CancellationToken cancellationToken = default);
    void Update(ReturnRequest request);
    IQueryable<ReturnRequest> Query();
}
