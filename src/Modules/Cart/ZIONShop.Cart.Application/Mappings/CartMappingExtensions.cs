using ZIONShop.Cart.Application.DTOs;
using ZIONShop.Cart.Domain.Entities;

namespace ZIONShop.Cart.Application.Mappings;

public static class CartMappingExtensions
{
    public static CartDto ToDto(this Domain.Entities.Cart cart)
    {
        var items = cart.Items
            .Select(i => new CartItemDto(i.Id, i.ProductId, i.ProductName, i.Quantity, i.UnitPrice, i.Subtotal, i.Currency))
            .ToList();
        var currency = items.FirstOrDefault()?.Currency ?? "USD";
        return new CartDto(cart.Id, cart.UserId, cart.AnonymousId, cart.Subtotal, currency, cart.TotalQuantity, items);
    }
}
