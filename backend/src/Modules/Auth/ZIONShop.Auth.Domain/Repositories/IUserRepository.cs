using ZIONShop.Auth.Domain.Entities;

namespace ZIONShop.Auth.Domain.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByRefreshTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default);
    Task SetLastLoginAsync(Guid userId, DateTime at, CancellationToken cancellationToken = default);
    void Update(User user);
}
