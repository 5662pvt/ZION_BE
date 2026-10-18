using ZIONShop.Reviews.Domain.Entities;

namespace ZIONShop.Reviews.Domain.Repositories;

public interface IProductQuestionRepository
{
    Task<ProductQuestion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(ProductQuestion question, CancellationToken cancellationToken = default);
    void Remove(ProductQuestion question);
    IQueryable<ProductQuestion> Query();
}
