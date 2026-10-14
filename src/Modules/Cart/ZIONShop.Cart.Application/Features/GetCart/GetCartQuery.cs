using MediatR;
using ZIONShop.Cart.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Cart.Application.Features.GetCart;

public record GetCartQuery(Guid? UserId, string? AnonymousId) : IRequest<Result<CartDto>>;
