using MediatR;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.PublishProduct;

public record PublishProductCommand(Guid Id) : IRequest<Result<ProductDto>>;
