namespace ZIONShop.Products.Application.DTOs;

public record ProductDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string Sku,
    decimal Price,
    string Currency,
    string Status,
    Guid? CategoryId,
    string? CategoryName,
    string? Brand,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<ProductOptionDto> Options,
    IReadOnlyList<ProductVariantDto> Variants,
    decimal MinPrice,
    decimal MaxPrice);

public record ProductImageDto(Guid Id, string Url, string? Alt, int DisplayOrder);

public record ProductOptionDto(string Name, IReadOnlyList<string> Values);

public record ProductVariantDto(
    Guid Id,
    string Sku,
    decimal Price,
    string Currency,
    string? Option1Value,
    string? Option2Value,
    string Label,
    Guid? ImageId,
    string? ImageUrl,
    bool IsActive);

public record ProductSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    string Sku,
    decimal Price,
    string Currency,
    string Status,
    string? CategoryName,
    string? PrimaryImageUrl,
    decimal MinPrice,
    decimal MaxPrice,
    int VariantCount);

public record ProductSuggestionDto(
    Guid Id,
    string Name,
    string Slug,
    string Sku,
    decimal Price,
    string Currency,
    string? ThumbnailUrl,
    decimal MinPrice,
    decimal MaxPrice,
    int VariantCount);
