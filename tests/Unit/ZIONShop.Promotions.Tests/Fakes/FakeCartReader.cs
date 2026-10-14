using ZIONShop.Contracts.Cart;

namespace ZIONShop.Promotions.Tests.Fakes;

public class FakeCartReader : ICartReader
{
    private readonly Dictionary<Guid, CartSnapshot> _carts = new();

    public FakeCartReader WithCart(Guid userId, string currency, params CartSnapshotLine[] lines)
    {
        _carts[userId] = new CartSnapshot(Guid.NewGuid(), currency, lines);
        return this;
    }

    public Task<CartSnapshot?> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult(_carts.TryGetValue(userId, out var cart) ? cart : null);
}
