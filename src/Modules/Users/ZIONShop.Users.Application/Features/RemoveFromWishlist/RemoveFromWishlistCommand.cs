using MediatR;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;

namespace ZIONShop.Users.Application.Features.RemoveFromWishlist;

public record RemoveFromWishlistCommand(Guid AuthUserId, Guid ProductId) : IRequest<Result<WishlistToggleDto>>;
