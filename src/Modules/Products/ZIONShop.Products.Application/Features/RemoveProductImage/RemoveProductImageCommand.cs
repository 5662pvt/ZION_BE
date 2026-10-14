using MediatR;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.RemoveProductImage;

public record RemoveProductImageCommand(Guid ProductId, Guid ImageId) : IRequest<Result>;
