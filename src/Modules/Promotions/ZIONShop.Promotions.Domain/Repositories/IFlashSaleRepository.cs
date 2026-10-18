using ZIONShop.Promotions.Domain.Entities;

namespace ZIONShop.Promotions.Domain.Repositories;

public interface IFlashSaleRepository
{
    Task<FlashSale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(FlashSale flashSale, CancellationToken cancellationToken = default);
    void Update(FlashSale flashSale);
    IQueryable<FlashSale> Query();
}
