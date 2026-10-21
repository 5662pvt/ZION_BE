using MediatR;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.CreateProduct;

public record CreateProductCommand(
    string Name,
    string Slug,
    string? Sku,
    decimal? Price,
    string? Currency,
    Guid? CategoryId,
    string? Description,
    string? Brand,
    IReadOnlyList<CreateProductImage>? Images,
    IReadOnlyList<CreateProductOptionInput>? Options = null,
    IReadOnlyList<CreateProductVariantInput>? Variants = null) : IRequest<Result<ProductDto>>;

public record CreateProductImage(string Url, string? Alt, int DisplayOrder);

public record CreateProductOptionInput(string Name, IReadOnlyList<string> Values);

public record CreateProductVariantInput(
    Guid? Id,
    string? Option1Value,
    string? Option2Value,
    string Sku,
    decimal Price,
    string Currency,
    bool IsActive,
    Guid? ImageId);
