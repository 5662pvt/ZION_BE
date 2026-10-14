using Microsoft.EntityFrameworkCore;
using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Repositories;
using ZIONShop.Orders.Infrastructure.Persistence;

namespace ZIONShop.Orders.Infrastructure.Repositories;

public class OrderIdempotencyKeyRepository : IOrderIdempotencyKeyRepository
{
    private readonly OrdersDbContext _db;

    public OrderIdempotencyKeyRepository(OrdersDbContext db) => _db = db;

    public Task<OrderIdempotencyKey?> GetAsync(Guid userId, string key, CancellationToken cancellationToken = default) =>
        _db.OrderIdempotencyKeys.FirstOrDefaultAsync(k => k.UserId == userId && k.Key == key, cancellationToken);

    public void Add(OrderIdempotencyKey entity) => _db.OrderIdempotencyKeys.Add(entity);
}
