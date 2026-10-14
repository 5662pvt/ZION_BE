using ZIONShop.Orders.Domain.Entities;

namespace ZIONShop.Orders.Domain.Repositories;

public interface IOrderIdempotencyKeyRepository
{
    Task<OrderIdempotencyKey?> GetAsync(Guid userId, string key, CancellationToken cancellationToken = default);
    void Add(OrderIdempotencyKey entity);
}
