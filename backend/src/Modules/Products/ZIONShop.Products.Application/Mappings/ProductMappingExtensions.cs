using ZIONShop.Products.Application.DTOs;
using ZIONShop.Products.Domain.Entities;

namespace ZIONShop.Products.Application.Mappings;

public static class ProductMappingExtensions
{
    public static ProductDto ToDto(this Product product, string? categoryName)
    {
        var images = product.Images
            .OrderBy(i => i.DisplayOrder)
            .Select(i => new ProductImageDto(i.Id, i.Url, i.Alt, i.DisplayOrder))
            .ToList();

        return new ProductDto(
            product.Id,
            product.Name,
            product.Slug,
            product.Description,
            product.Sku.Value,
            product.Price.Amount,
            product.Price.Currency,
            product.Status.ToString(),
            product.CategoryId,
            categoryName,
            product.Brand,
            images);
    }
}
