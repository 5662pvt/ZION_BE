namespace ZIONShop.Cart.Application.Interfaces;

public interface ICartUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
