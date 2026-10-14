using MediatR;
using ZIONShop.Contracts.Cart;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Products;
using ZIONShop.Contracts.Promotions;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.GetGiftPreview;

public class GetGiftPreviewQueryHandler : IRequestHandler<GetGiftPreviewQuery, Result<IReadOnlyList<GiftPreviewDto>>>
{
    private readonly ICartReader _cartReader;
    private readonly IGiftPromotionEvaluator _giftEvaluator;
    private readonly IStockAvailabilityReader _stockReader;
    private readonly IProductPriceLookup _priceLookup;

    public GetGiftPreviewQueryHandler(ICartReader cartReader, IGiftPromotionEvaluator giftEvaluator, IStockAvailabilityReader stockReader, IProductPriceLookup priceLookup)
    {
        _cartReader = cartReader;
        _giftEvaluator = giftEvaluator;
        _stockReader = stockReader;
        _priceLookup = priceLookup;
    }

    public async Task<Result<IReadOnlyList<GiftPreviewDto>>> Handle(GetGiftPreviewQuery request, CancellationToken cancellationToken)
    {
        var cart = await _cartReader.GetForUserAsync(request.UserId, cancellationToken);
        if (cart is null || cart.Lines.Count == 0)
            return Result.Success<IReadOnlyList<GiftPreviewDto>>(Array.Empty<GiftPreviewDto>());

        var lines = cart.Lines.Select(l => new CouponLine(l.ProductId, l.Quantity, l.UnitPrice * l.Quantity)).ToList();
        var subtotal = lines.Sum(l => l.LineTotal);

        var grants = await _giftEvaluator.QuoteGiftsAsync(new GiftRequest(request.UserId, lines, subtotal), cancellationToken);
        if (grants.Count == 0)
            return Result.Success<IReadOnlyList<GiftPreviewDto>>(Array.Empty<GiftPreviewDto>());

        var productIds = grants.Select(g => g.ProductId).Distinct().ToList();
        var available = await _stockReader.GetAvailableAsync(productIds, cancellationToken);
        var productInfo = await _priceLookup.GetManyAsync(productIds, cancellationToken);
        var cartQuantities = cart.Lines.GroupBy(l => l.ProductId).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        var items = grants.Select(g =>
        {
            productInfo.TryGetValue(g.ProductId, out var info);
            var availableQty = available.TryGetValue(g.ProductId, out var qty) ? qty : 0;
            var alreadyInCart = cartQuantities.TryGetValue(g.ProductId, out var cartQty) ? cartQty : 0;
            var isAvailable = availableQty >= g.Quantity + alreadyInCart;

            return new GiftPreviewDto(g.PromotionId, g.PromotionName, g.ProductId, info?.Name ?? string.Empty, info?.PrimaryImageUrl, g.Quantity, isAvailable);
        }).ToList();

        return Result.Success<IReadOnlyList<GiftPreviewDto>>(items);
    }
}
