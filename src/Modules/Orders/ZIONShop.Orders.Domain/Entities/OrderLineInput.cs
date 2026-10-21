namespace ZIONShop.Orders.Domain.Entities;

/// <summary>Input for <see cref="Order.Place"/>. VariantId ?? ProductId is the stock/price key.</summary>
public sealed record OrderLineInput(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    Guid? VariantId = null,
    string? VariantLabel = null,
    string? Sku = null)
{
    public Guid ResolvedVariantId => VariantId ?? ProductId;
}
