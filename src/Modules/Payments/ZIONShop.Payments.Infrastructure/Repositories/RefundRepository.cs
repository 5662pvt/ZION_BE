using Microsoft.EntityFrameworkCore;
using ZIONShop.Payments.Domain.Entities;
using ZIONShop.Payments.Domain.Repositories;
using ZIONShop.Payments.Infrastructure.Persistence;

namespace ZIONShop.Payments.Infrastructure.Repositories;

public class RefundRepository : IRefundRepository
{
    private readonly PaymentsDbContext _db;

    public RefundRepository(PaymentsDbContext db) => _db = db;

    public Task<Refund?> GetByReturnRequestIdAsync(Guid returnRequestId, CancellationToken cancellationToken = default) =>
        _db.Refunds.FirstOrDefaultAsync(r => r.ReturnRequestId == returnRequestId, cancellationToken);

    public async Task AddAsync(Refund refund, CancellationToken cancellationToken = default) =>
        await _db.Refunds.AddAsync(refund, cancellationToken);

    public IQueryable<Refund> Query() => _db.Refunds.AsQueryable();
}
