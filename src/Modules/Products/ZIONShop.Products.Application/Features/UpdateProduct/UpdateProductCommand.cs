using MediatR;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.UpdateProduct;

public record UpdateProductCommand(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    Guid? CategoryId,
    string? Brand) : IRequest<Result<ProductDto>>;
