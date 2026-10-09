using ZIONShop.Payments.Domain.Entities;

namespace ZIONShop.Payments.Domain.Repositories;

public interface IPayableOrderRepository
{
    Task<PayableOrder?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task AddAsync(PayableOrder order, CancellationToken cancellationToken = default);
}
