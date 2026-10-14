using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Repositories;

namespace ZIONShop.Orders.Tests.Fakes;

public class FakeOrderIdempotencyKeyRepository : IOrderIdempotencyKeyRepository
{
    private readonly List<OrderIdempotencyKey> _keys = new();

    public Task<OrderIdempotencyKey?> GetAsync(Guid userId, string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_keys.FirstOrDefault(k => k.UserId == userId && k.Key == key));

    public void Add(OrderIdempotencyKey entity) => _keys.Add(entity);
}
