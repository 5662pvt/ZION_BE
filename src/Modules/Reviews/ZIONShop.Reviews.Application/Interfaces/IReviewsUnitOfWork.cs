namespace ZIONShop.Reviews.Application.Interfaces;

public interface IReviewsUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
