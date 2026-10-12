using ZIONShop.Reviews.Domain.Entities;

namespace ZIONShop.Reviews.Domain.Repositories;

public interface IProductRatingSummaryRepository
{
    Task<ProductRatingSummary?> GetByIdAsync(Guid productId, CancellationToken cancellationToken = default);
    Task AddAsync(ProductRatingSummary summary, CancellationToken cancellationToken = default);
    IQueryable<ProductRatingSummary> Query();
}
