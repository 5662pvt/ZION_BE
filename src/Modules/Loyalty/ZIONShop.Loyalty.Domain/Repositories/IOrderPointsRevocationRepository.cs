using ZIONShop.Loyalty.Domain.Entities;

namespace ZIONShop.Loyalty.Domain.Repositories;

public interface IOrderPointsRevocationRepository
{
    Task<bool> ExistsAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task AddAsync(OrderPointsRevocation revocation, CancellationToken cancellationToken = default);
}
