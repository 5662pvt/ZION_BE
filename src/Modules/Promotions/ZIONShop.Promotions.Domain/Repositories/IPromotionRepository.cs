using ZIONShop.Promotions.Domain.Entities;

namespace ZIONShop.Promotions.Domain.Repositories;

public interface IPromotionRepository
{
    Task<Promotion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Promotion promotion, CancellationToken cancellationToken = default);
    void Update(Promotion promotion);
    IQueryable<Promotion> Query();
}
