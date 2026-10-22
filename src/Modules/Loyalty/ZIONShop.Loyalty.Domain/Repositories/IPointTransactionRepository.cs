using ZIONShop.Loyalty.Domain.Entities;

namespace ZIONShop.Loyalty.Domain.Repositories;

public interface IPointTransactionRepository
{
    Task AddAsync(PointTransaction transaction, CancellationToken cancellationToken = default);

    IQueryable<PointTransaction> Query();
}
