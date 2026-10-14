using MediatR;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.ArchiveProduct;

public record ArchiveProductCommand(Guid Id) : IRequest<Result<ProductDto>>;
