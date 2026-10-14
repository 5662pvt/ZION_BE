using Microsoft.EntityFrameworkCore;
using ZIONShop.Reviews.Domain.Entities;
using ZIONShop.Reviews.Domain.Repositories;
using ZIONShop.Reviews.Infrastructure.Persistence;

namespace ZIONShop.Reviews.Infrastructure.Repositories;

public class ProductRatingSummaryRepository : IProductRatingSummaryRepository
{
    private readonly ReviewsDbContext _db;

    public ProductRatingSummaryRepository(ReviewsDbContext db) => _db = db;

    public Task<ProductRatingSummary?> GetByIdAsync(Guid productId, CancellationToken cancellationToken = default) =>
        _db.ProductRatingSummaries.FirstOrDefaultAsync(s => s.ProductId == productId, cancellationToken);

    public async Task AddAsync(ProductRatingSummary summary, CancellationToken cancellationToken = default) =>
        await _db.ProductRatingSummaries.AddAsync(summary, cancellationToken);

    public IQueryable<ProductRatingSummary> Query() => _db.ProductRatingSummaries.AsQueryable();
}
