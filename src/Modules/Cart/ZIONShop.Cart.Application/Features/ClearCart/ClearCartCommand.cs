using MediatR;
using ZIONShop.Cart.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Cart.Application.Features.ClearCart;

public record ClearCartCommand(Guid? UserId, string? AnonymousId) : IRequest<Result<CartDto>>;
