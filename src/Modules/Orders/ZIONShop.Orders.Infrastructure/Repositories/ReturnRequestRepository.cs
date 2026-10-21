using Microsoft.EntityFrameworkCore;
using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.Repositories;
using ZIONShop.Orders.Infrastructure.Persistence;

namespace ZIONShop.Orders.Infrastructure.Repositories;

public class ReturnRequestRepository : IReturnRequestRepository
{
    private readonly OrdersDbContext _db;

    public ReturnRequestRepository(OrdersDbContext db) => _db = db;

    public Task<ReturnRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.ReturnRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<ReturnRequest?> GetLatestByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        _db.ReturnRequests
            .Where(r => r.OrderId == orderId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ReturnRequest?> GetActiveByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        _db.ReturnRequests.FirstOrDefaultAsync(r => r.OrderId == orderId && r.Status != ReturnRequestStatus.Rejected, cancellationToken);

    public async Task AddAsync(ReturnRequest request, CancellationToken cancellationToken = default) =>
        await _db.ReturnRequests.AddAsync(request, cancellationToken);

    public void Update(ReturnRequest request)
    {
        if (_db.Entry(request).State == EntityState.Detached)
            _db.ReturnRequests.Update(request);
    }

    public IQueryable<ReturnRequest> Query() => _db.ReturnRequests.AsQueryable();
}
