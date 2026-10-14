namespace ZIONShop.Users.Application.Interfaces;

public interface IUsersUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
