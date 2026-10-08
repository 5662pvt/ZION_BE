using Microsoft.EntityFrameworkCore;
using ZIONShop.Cart.Infrastructure.Persistence;
using ZIONShop.Contracts.Cart;

namespace ZIONShop.Cart.Infrastructure.Readers;

public class CartReader : ICartReader
{
    private readonly CartDbContext _db;

    public CartReader(CartDbContext db) => _db = db;

    public async Task<CartSnapshot?> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cart = await _db.Carts.AsNoTracking()
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (cart is null) return null;

        var lines = cart.Items
            .Select(i => new CartSnapshotLine(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice))
            .ToList();

        var currency = cart.Items.Select(i => i.Currency).FirstOrDefault() ?? "VND";
        return new CartSnapshot(cart.Id, currency, lines);
    }
}
