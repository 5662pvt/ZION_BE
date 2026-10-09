using Microsoft.EntityFrameworkCore;
using ZIONShop.Payments.Domain.Entities;
using ZIONShop.Payments.Domain.Repositories;
using ZIONShop.Payments.Infrastructure.Persistence;

namespace ZIONShop.Payments.Infrastructure.Repositories;

public class PayableOrderRepository : IPayableOrderRepository
{
    private readonly PaymentsDbContext _db;

    public PayableOrderRepository(PaymentsDbContext db) => _db = db;

    public Task<PayableOrder?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        _db.PayableOrders.FirstOrDefaultAsync(p => p.OrderId == orderId, cancellationToken);

    public async Task AddAsync(PayableOrder order, CancellationToken cancellationToken = default) =>
        await _db.PayableOrders.AddAsync(order, cancellationToken);
}
