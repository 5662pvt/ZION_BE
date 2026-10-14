using Microsoft.EntityFrameworkCore;
using ZIONShop.Payments.Domain.Entities;
using ZIONShop.Payments.Domain.Enums;
using ZIONShop.Payments.Domain.Repositories;
using ZIONShop.Payments.Infrastructure.Persistence;

namespace ZIONShop.Payments.Infrastructure.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly PaymentsDbContext _db;

    public PaymentRepository(PaymentsDbContext db) => _db = db;

    public Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Payments.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Payment?> GetByIntentIdAsync(string intentId, CancellationToken cancellationToken = default) =>
        _db.Payments.FirstOrDefaultAsync(p => p.IntentId == intentId, cancellationToken);

    public Task<Payment?> GetByIdempotencyKeyAsync(Guid userId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        _db.Payments.FirstOrDefaultAsync(p => p.UserId == userId && p.IdempotencyKey == idempotencyKey, cancellationToken);

    public Task<Payment?> GetPendingByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        _db.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId && p.Status == PaymentStatus.Pending, cancellationToken);

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken = default) =>
        await _db.Payments.AddAsync(payment, cancellationToken);
}
