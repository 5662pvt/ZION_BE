using MediatR;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.AddProductImage;

public record AddProductImageCommand(
    Guid ProductId,
    Stream Content,
    string FileName,
    string ContentType,
    long Length,
    string? Alt) : IRequest<Result<ProductImageDto>>;
