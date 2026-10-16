using ZIONShop.Payments.Domain.Entities;

namespace ZIONShop.Payments.Domain.Repositories;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Payment?> GetByIntentIdAsync(string intentId, CancellationToken cancellationToken = default);
    Task<Payment?> GetByIdempotencyKeyAsync(Guid userId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<Payment?> GetPendingByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task AddAsync(Payment payment, CancellationToken cancellationToken = default);
    IQueryable<Payment> Query();
}
