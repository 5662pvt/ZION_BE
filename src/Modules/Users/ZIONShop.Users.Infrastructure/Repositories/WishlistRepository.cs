using Microsoft.EntityFrameworkCore;
using ZIONShop.Users.Domain.Entities;
using ZIONShop.Users.Domain.Repositories;
using ZIONShop.Users.Infrastructure.Persistence;

namespace ZIONShop.Users.Infrastructure.Repositories;

public class WishlistRepository : IWishlistRepository
{
    private readonly UsersDbContext _db;

    public WishlistRepository(UsersDbContext db) => _db = db;

    public Task<WishlistItem?> GetAsync(Guid userId, Guid productId, CancellationToken cancellationToken = default) =>
        _db.WishlistItems.FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId, cancellationToken);

    public Task<int> CountAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _db.WishlistItems.CountAsync(w => w.UserId == userId, cancellationToken);

    public async Task AddAsync(WishlistItem item, CancellationToken cancellationToken = default) =>
        await _db.WishlistItems.AddAsync(item, cancellationToken);

    public void Remove(WishlistItem item) => _db.WishlistItems.Remove(item);

    public IQueryable<WishlistItem> Query() => _db.WishlistItems.AsQueryable();
}
