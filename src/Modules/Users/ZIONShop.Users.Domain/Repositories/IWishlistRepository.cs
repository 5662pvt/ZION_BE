using ZIONShop.Users.Domain.Entities;

namespace ZIONShop.Users.Domain.Repositories;

public interface IWishlistRepository
{
    Task<WishlistItem?> GetAsync(Guid userId, Guid productId, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(WishlistItem item, CancellationToken cancellationToken = default);
    void Remove(WishlistItem item);
    IQueryable<WishlistItem> Query();
}
