using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.Promotions.Domain.Entities;

namespace ZIONShop.Promotions.Application.Mappings;

public static class FlashSaleMappingExtensions
{
    public static FlashSaleItemDto ToDto(this FlashSaleItem item, string? sku = null, string? variantLabel = null) =>
        new(item.Id, item.ProductId, item.VariantId, variantLabel, sku, item.FlashPrice, item.AllocatedQuantity, item.SoldQuantity, item.Available);

    public static FlashSaleSummaryDto ToSummaryDto(this FlashSale flashSale, int itemCount) =>
        new(flashSale.Id, flashSale.Name, flashSale.StartsAt, flashSale.EndsAt, flashSale.IsActive, itemCount);

    public static FlashSaleDto ToDto(this FlashSale flashSale, IReadOnlyList<FlashSaleItemDto> items) =>
        new(flashSale.Id, flashSale.Name, flashSale.StartsAt, flashSale.EndsAt, flashSale.IsActive, items);
}
