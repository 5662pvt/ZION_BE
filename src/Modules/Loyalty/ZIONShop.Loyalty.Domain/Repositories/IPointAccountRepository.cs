using ZIONShop.Loyalty.Domain.Entities;

namespace ZIONShop.Loyalty.Domain.Repositories;

public interface IPointAccountRepository
{
    Task<PointAccount?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddAsync(PointAccount account, CancellationToken cancellationToken = default);

    void Update(PointAccount account);
}
