using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Cart.Domain.Entities;

public class Cart : AggregateRoot
{
    public const int MaxItems = 50;

    private readonly List<CartItem> _items = new();

    private Cart() { }

    public Guid? UserId { get; private set; }
    public string? AnonymousId { get; private set; }
    public DateTime ExpiresAt { get; private set; } = DateTime.UtcNow.AddDays(30);

    public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();

    public decimal Subtotal => _items.Sum(i => i.Subtotal);
    public int TotalQuantity => _items.Sum(i => i.Quantity);

    public static Cart CreateForUser(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId
    };

    public static Cart CreateForGuest(string anonymousId) => new()
    {
        Id = Guid.NewGuid(),
        AnonymousId = anonymousId
    };

    public CartItem AddItem(Guid productId, Guid variantId, string productName, int quantity, decimal unitPrice, string currency, string? variantLabel = null, string? sku = null)
    {
        if (_items.Count >= MaxItems && _items.All(i => i.VariantId != variantId))
            throw new InvalidOperationException($"Cart cannot contain more than {MaxItems} distinct items.");

        var existing = _items.FirstOrDefault(i => i.VariantId == variantId);
        if (existing is not null)
        {
            existing.Increase(quantity);
            return existing;
        }

        var item = CartItem.Create(Id, productId, variantId, productName, quantity, unitPrice, currency, variantLabel, sku);
        _items.Add(item);
        return item;
    }

    public CartItem? UpdateItem(Guid itemId, int newQuantity)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId);
        item?.UpdateQuantity(newQuantity);
        return item;
    }

    public bool RemoveItem(Guid itemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item is null) return false;
        _items.Remove(item);
        return true;
    }

    public void Clear() => _items.Clear();

    public void AssignToUser(Guid userId)
    {
        UserId = userId;
        AnonymousId = null;
    }

    public void MergeFrom(Cart guest)
    {
        foreach (var item in guest.Items.ToList())
            AddItem(item.ProductId, item.VariantId, item.ProductName, item.Quantity, item.UnitPrice, item.Currency, item.VariantLabel, item.Sku);
    }
}
