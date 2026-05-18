using MediatR;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.CreateProduct;

public record CreateProductCommand(
    string Name,
    string Slug,
    string Sku,
    decimal Price,
    string Currency,
    Guid? CategoryId,
    string? Description,
    string? Brand,
    IReadOnlyList<CreateProductImage>? Images) : IRequest<Result<ProductDto>>;

public record CreateProductImage(string Url, string? Alt, int DisplayOrder);
