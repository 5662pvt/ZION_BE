namespace ZIONShop.Chat.Application.Interfaces;

public interface IChatUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
