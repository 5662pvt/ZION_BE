namespace ZIONShop.Auth.Application.Interfaces;

public interface IAuthUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
