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

    public void Update(DomainCart cart)
    {
        // Suspend auto change-detection: EF resolves a never-before-tracked entity's
        // state off its key as soon as DetectChanges runs (non-default key => Modified),
        // even on a bare Entry() lookup. New CartItems always carry a client-generated
        // Guid key, so that heuristic alone (via DbSet.Update's graph walk, or even an
        // incidental DetectChanges from Entry()/State) would misclassify them as
        // Modified and issue an UPDATE for a row that was never inserted. Looking them
        // up with detection suspended gets a true Detached/Added read instead.
        var wasAutoDetectEnabled = _db.ChangeTracker.AutoDetectChangesEnabled;
        _db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            var cartEntry = _db.Entry(cart);
            if (cartEntry.State == EntityState.Detached)
            {
                _db.Carts.Update(cart);
                return;
            }

            // Cart (and its originally loaded items) are already tracked, so EF's own
            // change detection, once re-enabled, picks up scalar edits and removed
            // items on SaveChanges. Only newly added items need to be marked explicitly.
            foreach (var item in cart.Items)
            {
                var itemEntry = _db.Entry(item);
                if (itemEntry.State == EntityState.Detached)
                    itemEntry.State = EntityState.Added;
            }
        }
        finally
        {
            _db.ChangeTracker.AutoDetectChangesEnabled = wasAutoDetectEnabled;
        }
    }

    public void Remove(DomainCart cart) => _db.Carts.Remove(cart);
}
