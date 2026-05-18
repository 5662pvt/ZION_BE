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
    IReadOnlyList<ProductImageDto> Images);

public record ProductImageDto(Guid Id, string Url, string? Alt, int DisplayOrder);

public record ProductSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    string Sku,
    decimal Price,
    string Currency,
    string Status,
    string? CategoryName,
    string? PrimaryImageUrl);
