using MediatR;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;

namespace ZIONShop.Users.Application.Features.AddToWishlist;

public record AddToWishlistCommand(Guid AuthUserId, Guid ProductId) : IRequest<Result<WishlistToggleDto>>;
