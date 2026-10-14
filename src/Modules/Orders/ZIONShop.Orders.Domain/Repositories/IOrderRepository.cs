using ZIONShop.Orders.Domain.Entities;

namespace ZIONShop.Orders.Domain.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Order order, CancellationToken cancellationToken = default);
    void Update(Order order);
    IQueryable<Order> Query();
}
