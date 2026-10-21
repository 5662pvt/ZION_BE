using ZIONShop.Cart.Application.DTOs;
using ZIONShop.Contracts.Products;

namespace ZIONShop.Cart.Application.Mappings;

public static class CartMappingExtensions
{
    public static CartDto ToDto(this Domain.Entities.Cart cart)
    {
        var items = cart.Items
            .Select(i => new CartItemDto(i.Id, i.ProductId, i.VariantId, i.ProductName, i.VariantLabel, i.Sku, i.Quantity, i.UnitPrice, i.Subtotal, i.Currency, null, null))
            .ToList();
        var currency = items.FirstOrDefault()?.Currency ?? "USD";
        return new CartDto(cart.Id, cart.UserId, cart.AnonymousId, cart.Subtotal, currency, cart.TotalQuantity, items);
    }

    public static async Task<CartDto> ToEnrichedDtoAsync(this Domain.Entities.Cart cart, IProductPriceLookup prices, CancellationToken cancellationToken)
    {
        var dto = cart.ToDto();
        if (dto.Items.Count == 0) return dto;

        var variantIds = dto.Items.Select(i => i.VariantId).Distinct().ToList();
        var infos = await prices.GetManyAsync(variantIds, cancellationToken);

        var items = dto.Items
            .Select(i => infos.TryGetValue(i.VariantId, out var info)
                ? i with { ProductSlug = info.Slug, ImageUrl = info.VariantImageUrl ?? info.PrimaryImageUrl }
                : i)
            .ToList();

        return dto with { Items = items };
    }
}
