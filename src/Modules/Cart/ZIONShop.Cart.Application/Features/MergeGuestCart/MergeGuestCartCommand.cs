using MediatR;
using ZIONShop.Cart.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Cart.Application.Features.MergeGuestCart;

public record MergeGuestCartCommand(Guid UserId, string? AnonymousId) : IRequest<Result<CartDto>>;
