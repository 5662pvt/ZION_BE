using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Common.Pagination;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Products;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;
using ZIONShop.Users.Domain.Repositories;

namespace ZIONShop.Users.Application.Features.GetMyWishlist;

public class GetMyWishlistQueryHandler : IRequestHandler<GetMyWishlistQuery, Result<PagedResult<WishlistItemDto>>>
{
    private readonly IWishlistRepository _wishlist;
    private readonly IProductPriceLookup _prices;
    private readonly IStockAvailabilityReader _stock;

    public GetMyWishlistQueryHandler(IWishlistRepository wishlist, IProductPriceLookup prices, IStockAvailabilityReader stock)
    {
        _wishlist = wishlist;
        _prices = prices;
        _stock = stock;
    }

    public async Task<Result<PagedResult<WishlistItemDto>>> Handle(GetMyWishlistQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        // Wishlist is capped at 200 items (AddToWishlistCommandHandler), so loading it all and filtering
        // before paging is bounded -- otherwise unpublished products dropped after a DB-level Skip/Take
        // would shrink a page below pageSize and make `total` count items the caller never sees.
        var allItems = await _wishlist.Query()
            .Where(w => w.UserId == request.AuthUserId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);

        var productIds = allItems.Select(w => w.ProductId).Distinct().ToList();
        var priceInfos = await _prices.GetManyAsync(productIds, cancellationToken);
        var available = await _stock.GetAvailableAsync(productIds, cancellationToken);

        var visibleItems = allItems
            .Where(w => priceInfos.TryGetValue(w.ProductId, out var info) && info.IsAvailable)
            .ToList();

        var total = visibleItems.Count;
        var dtos = visibleItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(w =>
            {
                var info = priceInfos[w.ProductId];
                var inStock = available.TryGetValue(w.ProductId, out var qty) && qty > 0;
                return new WishlistItemDto(w.ProductId, info.Name, info.Slug, info.UnitPrice, info.Currency, info.PrimaryImageUrl, inStock, w.CreatedAt);
            })
            .ToList();

        return Result.Success(PagedResult<WishlistItemDto>.Create(dtos, page, pageSize, total));
    }
}
