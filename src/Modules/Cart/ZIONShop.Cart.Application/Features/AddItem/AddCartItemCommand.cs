using MediatR;
using ZIONShop.Cart.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Cart.Application.Features.AddItem;

public record AddCartItemCommand(Guid? UserId, string? AnonymousId, Guid? ProductId, Guid? VariantId, int Quantity) : IRequest<Result<CartDto>>;
