using MediatR;
using ZIONShop.Cart.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Cart.Application.Features.UpdateItem;

public record UpdateCartItemCommand(Guid? UserId, string? AnonymousId, Guid ItemId, int Quantity) : IRequest<Result<CartDto>>;
