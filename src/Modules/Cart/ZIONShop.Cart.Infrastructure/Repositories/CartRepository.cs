using Microsoft.EntityFrameworkCore;
using ZIONShop.Cart.Domain.Repositories;
using ZIONShop.Cart.Infrastructure.Persistence;
using DomainCart = ZIONShop.Cart.Domain.Entities.Cart;

namespace ZIONShop.Cart.Infrastructure.Repositories;

public class CartRepository : ICartRepository
{
    private readonly CartDbContext _db;

    public CartRepository(CartDbContext db) => _db = db;

    public Task<DomainCart?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

    public Task<DomainCart?> GetByAnonymousIdAsync(string anonymousId, CancellationToken cancellationToken = default) =>
        _db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.AnonymousId == anonymousId, cancellationToken);

    public Task<DomainCart?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task AddAsync(DomainCart cart, CancellationToken cancellationToken = default) =>
        await _db.Carts.AddAsync(cart, cancellationToken);

    public void Update(DomainCart cart) => _db.Carts.Update(cart);

    public void Remove(DomainCart cart) => _db.Carts.Remove(cart);
}
