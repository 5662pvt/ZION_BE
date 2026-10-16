using MediatR;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;
using ZIONShop.Users.Application.Interfaces;
using ZIONShop.Users.Domain.Repositories;

namespace ZIONShop.Users.Application.Features.RemoveFromWishlist;

public class RemoveFromWishlistCommandHandler : IRequestHandler<RemoveFromWishlistCommand, Result<WishlistToggleDto>>
{
    private readonly IWishlistRepository _wishlist;
    private readonly IUsersUnitOfWork _uow;

    public RemoveFromWishlistCommandHandler(IWishlistRepository wishlist, IUsersUnitOfWork uow)
    {
        _wishlist = wishlist;
        _uow = uow;
    }

    public async Task<Result<WishlistToggleDto>> Handle(RemoveFromWishlistCommand request, CancellationToken cancellationToken)
    {
        var existing = await _wishlist.GetAsync(request.AuthUserId, request.ProductId, cancellationToken);
        if (existing is null)
            return Result.Success(new WishlistToggleDto(request.ProductId, false));

        _wishlist.Remove(existing);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success(new WishlistToggleDto(request.ProductId, false));
    }
}
