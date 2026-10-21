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
    private readonly IProductStockSummaryReader _stock;

    public GetMyWishlistQueryHandler(IWishlistRepository wishlist, IProductPriceLookup prices, IProductStockSummaryReader stock)
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
        // Product-level summary (not GetManyAsync's variant-keyed price lookup) and product-level stock
        // (summed across every variant) -- a multi-variant product has no single "the" variant, so looking
        // either of these up by product id against the variant-keyed APIs always reported it unavailable/out
        // of stock, even when perfectly purchasable.
        var summaries = await _prices.GetProductSummaryAsync(productIds, cancellationToken);
        var available = await _stock.GetAvailableByProductAsync(productIds, cancellationToken);

        var visibleItems = allItems
            .Where(w => summaries.TryGetValue(w.ProductId, out var info) && info.IsPublished)
            .ToList();

        var total = visibleItems.Count;
        var dtos = visibleItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(w =>
            {
                var info = summaries[w.ProductId];
                var inStock = available.TryGetValue(w.ProductId, out var qty) && qty > 0;
                return new WishlistItemDto(w.ProductId, info.Name, info.Slug, info.MinPrice, info.Currency, info.PrimaryImageUrl, inStock, w.CreatedAt, info.MinPrice, info.MaxPrice, info.VariantCount);
            })
            .ToList();

        return Result.Success(PagedResult<WishlistItemDto>.Create(dtos, page, pageSize, total));
    }
}
