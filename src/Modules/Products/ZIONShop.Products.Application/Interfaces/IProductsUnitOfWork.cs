namespace ZIONShop.Products.Application.Interfaces;

public interface IProductsUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
