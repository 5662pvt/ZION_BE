using MediatR;
using ZIONShop.Contracts.Products;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;
using ZIONShop.Users.Application.Interfaces;
using ZIONShop.Users.Domain.Entities;
using ZIONShop.Users.Domain.Exceptions;
using ZIONShop.Users.Domain.Repositories;

namespace ZIONShop.Users.Application.Features.AddToWishlist;

public class AddToWishlistCommandHandler : IRequestHandler<AddToWishlistCommand, Result<WishlistToggleDto>>
{
    private const int MaxWishlistItems = 200;

    private readonly IWishlistRepository _wishlist;
    private readonly IProductPriceLookup _prices;
    private readonly IUsersUnitOfWork _uow;

    public AddToWishlistCommandHandler(IWishlistRepository wishlist, IProductPriceLookup prices, IUsersUnitOfWork uow)
    {
        _wishlist = wishlist;
        _prices = prices;
        _uow = uow;
    }

    public async Task<Result<WishlistToggleDto>> Handle(AddToWishlistCommand request, CancellationToken cancellationToken)
    {
        var existing = await _wishlist.GetAsync(request.AuthUserId, request.ProductId, cancellationToken);
        if (existing is not null)
            return Result.Success(new WishlistToggleDto(request.ProductId, true));

        var summaries = await _prices.GetProductSummaryAsync(new[] { request.ProductId }, cancellationToken);
        if (!summaries.TryGetValue(request.ProductId, out var summary) || !summary.IsPublished)
            return Result.Failure<WishlistToggleDto>(UsersErrors.ProductNotFound);

        var count = await _wishlist.CountAsync(request.AuthUserId, cancellationToken);
        if (count >= MaxWishlistItems)
            return Result.Failure<WishlistToggleDto>(UsersErrors.WishlistFull);

        var item = WishlistItem.Create(request.AuthUserId, request.ProductId);
        await _wishlist.AddAsync(item, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success(new WishlistToggleDto(request.ProductId, true));
    }
}
