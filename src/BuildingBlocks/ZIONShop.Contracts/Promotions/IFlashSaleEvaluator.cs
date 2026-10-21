namespace ZIONShop.Contracts.Promotions;

/// <summary>VariantId ?? ProductId is the stock/price key.</summary>
public sealed record FlashSaleLine(Guid ProductId, int Quantity, Guid? VariantId = null);

/// <summary>VariantId ?? ProductId is the stock/price key.</summary>
public sealed record FlashSaleQuote(Guid ProductId, Guid FlashSaleItemId, decimal FlashUnitPrice, Guid? VariantId = null);

public interface IFlashSaleEvaluator
{
    /// <summary>Read-only quote for the storefront preview and for Orders before reserving.</summary>
    Task<IReadOnlyList<FlashSaleQuote>> QuoteAsync(IReadOnlyList<FlashSaleLine> lines, CancellationToken ct = default);

    /// <summary>
    /// Atomically reserves each line's full quantity against its matching flash-sale item (all-or-nothing
    /// per line, idempotent per (flashSaleItemId, orderId)). A line absent from the result means it is not
    /// flash-priced (quota exhausted or no active flash sale for that product) -- the caller falls back to
    /// the regular price for that line; it does NOT fail the whole order.
    /// </summary>
    Task<IReadOnlyList<FlashSaleQuote>> ReserveAsync(IReadOnlyList<FlashSaleLine> lines, Guid orderId, Guid userId, CancellationToken ct = default);

    /// <summary>Releases all reservations made for orderId (idempotent; no-op if none).</summary>
    Task ReleaseAsync(Guid orderId, CancellationToken ct = default);
}
