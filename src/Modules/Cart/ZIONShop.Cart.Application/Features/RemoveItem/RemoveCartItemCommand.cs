using MediatR;
using ZIONShop.Cart.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Cart.Application.Features.RemoveItem;

public record RemoveCartItemCommand(Guid? UserId, string? AnonymousId, Guid ItemId) : IRequest<Result<CartDto>>;
