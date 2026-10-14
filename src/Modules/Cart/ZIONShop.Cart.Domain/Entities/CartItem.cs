using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Cart.Domain.Entities;

public class CartItem : BaseEntity
{
    private CartItem() { }

    public Guid CartId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public string Currency { get; private set; } = "USD";

    public decimal Subtotal => Quantity * UnitPrice;

    public static CartItem Create(Guid cartId, Guid productId, string productName, int quantity, decimal unitPrice, string currency)
    {
        if (quantity <= 0) throw new ArgumentException("Quantity must be positive", nameof(quantity));
        if (unitPrice < 0) throw new ArgumentException("Unit price cannot be negative", nameof(unitPrice));
        return new CartItem
        {
            Id = Guid.NewGuid(),
            CartId = cartId,
            ProductId = productId,
            ProductName = productName,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Currency = currency
        };
    }

    public void UpdateQuantity(int newQuantity)
    {
        if (newQuantity <= 0) throw new ArgumentException("Quantity must be positive", nameof(newQuantity));
        Quantity = newQuantity;
    }

    public void Increase(int delta)
    {
        if (delta <= 0) throw new ArgumentException("Delta must be positive", nameof(delta));
        Quantity += delta;
    }
}
