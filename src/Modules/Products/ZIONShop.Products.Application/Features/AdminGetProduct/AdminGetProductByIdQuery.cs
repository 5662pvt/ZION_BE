using MediatR;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.AdminGetProduct;

public record AdminGetProductByIdQuery(Guid Id) : IRequest<Result<ProductDto>>;
