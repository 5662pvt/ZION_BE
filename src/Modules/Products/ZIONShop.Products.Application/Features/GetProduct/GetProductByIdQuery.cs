using MediatR;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.GetProduct;

public record GetProductByIdQuery(Guid Id) : IRequest<Result<ProductDto>>;
