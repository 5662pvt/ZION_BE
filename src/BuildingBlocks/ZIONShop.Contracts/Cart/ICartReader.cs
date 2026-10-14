namespace ZIONShop.Contracts.Cart;

public record CartSnapshot(Guid CartId, string Currency, IReadOnlyList<CartSnapshotLine> Lines);

public record CartSnapshotLine(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice);

public interface ICartReader
{
    Task<CartSnapshot?> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
