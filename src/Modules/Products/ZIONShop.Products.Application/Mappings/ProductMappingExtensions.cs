using ZIONShop.Products.Application.DTOs;
using ZIONShop.Products.Domain.Entities;

namespace ZIONShop.Products.Application.Mappings;

public static class ProductMappingExtensions
{
    public static ProductDto ToDto(this Product product, string? categoryName, bool includeInactiveVariants = false)
    {
        var images = product.Images
            .OrderBy(i => i.DisplayOrder)
            .Select(i => new ProductImageDto(i.Id, i.Url, i.Alt, i.DisplayOrder))
            .ToList();

        var imageUrlsById = product.Images.ToDictionary(i => i.Id, i => i.Url);

        var variantSource = includeInactiveVariants ? product.Variants : product.Variants.Where(v => v.IsActive);
        var variants = variantSource
            .OrderBy(v => v.DisplayOrder)
            .Select(v => new ProductVariantDto(
                v.Id,
                v.Sku.Value,
                v.Price.Amount,
                v.Price.Currency,
                v.Option1Value,
                v.Option2Value,
                v.Label,
                v.ImageId,
                v.ImageId.HasValue && imageUrlsById.TryGetValue(v.ImageId.Value, out var url) ? url : null,
                v.IsActive))
            .ToList();

        var options = product.Options
            .OrderBy(o => o.Position)
            .Select(o => new ProductOptionDto(o.Name, o.Values))
            .ToList();

        var representative = product.Variants.Where(v => v.IsActive).OrderBy(v => v.Price.Amount).ThenBy(v => v.DisplayOrder).FirstOrDefault()
            ?? product.Variants.OrderBy(v => v.DisplayOrder).FirstOrDefault();

        return new ProductDto(
            product.Id,
            product.Name,
            product.Slug,
            product.Description,
            representative?.Sku.Value ?? string.Empty,
            representative?.Price.Amount ?? 0m,
            representative?.Price.Currency ?? "VND",
            product.Status.ToString(),
            product.CategoryId,
            categoryName,
            product.Brand,
            images,
            options,
            variants,
            product.MinPrice,
            product.MaxPrice);
    }
}
