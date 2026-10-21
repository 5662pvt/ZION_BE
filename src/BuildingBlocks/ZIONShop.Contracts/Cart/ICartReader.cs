namespace ZIONShop.Contracts.Cart;

public record CartSnapshot(Guid CartId, string Currency, IReadOnlyList<CartSnapshotLine> Lines);

/// <summary>VariantId ?? ProductId is the stock/price key.</summary>
public record CartSnapshotLine(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice, Guid? VariantId = null);

public interface ICartReader
{
    Task<CartSnapshot?> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
