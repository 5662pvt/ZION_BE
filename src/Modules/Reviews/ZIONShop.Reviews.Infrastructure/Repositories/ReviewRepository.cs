using Microsoft.EntityFrameworkCore;
using ZIONShop.Reviews.Domain.Entities;
using ZIONShop.Reviews.Domain.Repositories;
using ZIONShop.Reviews.Infrastructure.Persistence;

namespace ZIONShop.Reviews.Infrastructure.Repositories;

public class ReviewRepository : IReviewRepository
{
    private readonly ReviewsDbContext _db;

    public ReviewRepository(ReviewsDbContext db) => _db = db;

    public Task<Review?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Reviews.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Review?> GetByUserAndProductAsync(Guid userId, Guid productId, CancellationToken cancellationToken = default) =>
        _db.Reviews.FirstOrDefaultAsync(r => r.UserId == userId && r.ProductId == productId, cancellationToken);

    public async Task AddAsync(Review review, CancellationToken cancellationToken = default) =>
        await _db.Reviews.AddAsync(review, cancellationToken);

    public void Remove(Review review) => _db.Reviews.Remove(review);

    public IQueryable<Review> Query() => _db.Reviews.AsQueryable();
}
