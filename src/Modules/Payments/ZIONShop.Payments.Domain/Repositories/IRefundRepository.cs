using ZIONShop.Payments.Domain.Entities;

namespace ZIONShop.Payments.Domain.Repositories;

public interface IRefundRepository
{
    Task<Refund?> GetByReturnRequestIdAsync(Guid returnRequestId, CancellationToken cancellationToken = default);
    Task AddAsync(Refund refund, CancellationToken cancellationToken = default);
    IQueryable<Refund> Query();
}
