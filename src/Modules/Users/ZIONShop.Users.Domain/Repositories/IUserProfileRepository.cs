using ZIONShop.Users.Domain.Entities;

namespace ZIONShop.Users.Domain.Repositories;

public interface IUserProfileRepository
{
    Task<UserProfile?> GetByAuthUserIdAsync(Guid authUserId, CancellationToken cancellationToken = default);
    Task<UserProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsForAuthUserAsync(Guid authUserId, CancellationToken cancellationToken = default);
    Task AddAsync(UserProfile profile, CancellationToken cancellationToken = default);
    void Update(UserProfile profile);
}
