using Microsoft.EntityFrameworkCore;
using ZIONShop.Reviews.Domain.Entities;
using ZIONShop.Reviews.Domain.Repositories;
using ZIONShop.Reviews.Infrastructure.Persistence;

namespace ZIONShop.Reviews.Infrastructure.Repositories;

public class ProductQuestionRepository : IProductQuestionRepository
{
    private readonly ReviewsDbContext _db;

    public ProductQuestionRepository(ReviewsDbContext db) => _db = db;

    public Task<ProductQuestion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.ProductQuestions.FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

    public async Task AddAsync(ProductQuestion question, CancellationToken cancellationToken = default) =>
        await _db.ProductQuestions.AddAsync(question, cancellationToken);

    public void Remove(ProductQuestion question) => _db.ProductQuestions.Remove(question);

    public IQueryable<ProductQuestion> Query() => _db.ProductQuestions.AsQueryable();
}
