using MediatR;
using ZIONShop.Contracts.Products;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.Promotions.Application.Interfaces;
using ZIONShop.Promotions.Application.Mappings;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Enums;
using ZIONShop.Promotions.Domain.Exceptions;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.UpdatePromotion;

public class UpdatePromotionCommandHandler : IRequestHandler<UpdatePromotionCommand, Result<PromotionDto>>
{
    private readonly IPromotionRepository _promotions;
    private readonly ICouponCodeRepository _coupons;
    private readonly IPromotionsUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;
    private readonly IProductCatalogReader _catalogReader;
    private readonly IProductPriceLookup _priceLookup;

    public UpdatePromotionCommandHandler(IPromotionRepository promotions, ICouponCodeRepository coupons, IPromotionsUnitOfWork uow, TimeProvider timeProvider, IProductCatalogReader catalogReader, IProductPriceLookup priceLookup)
    {
        _promotions = promotions;
        _coupons = coupons;
        _uow = uow;
        _timeProvider = timeProvider;
        _catalogReader = catalogReader;
        _priceLookup = priceLookup;
    }

    public async Task<Result<PromotionDto>> Handle(UpdatePromotionCommand request, CancellationToken cancellationToken)
    {
        var promotion = await _promotions.GetByIdAsync(request.PromotionId, cancellationToken);
        if (promotion is null) return Result.Failure<PromotionDto>(PromotionsErrors.PromotionNotFound);

        if (!Enum.TryParse<PromotionType>(request.Type, ignoreCase: true, out var type))
            return Result.Failure<PromotionDto>(PromotionsErrors.InvalidPromotionType);

        if (!Enum.TryParse<PromotionScope>(request.Scope, ignoreCase: true, out var scope))
            return Result.Failure<PromotionDto>(PromotionsErrors.InvalidPromotionScope);

        var value = type is PromotionType.FreeShipping or PromotionType.Gift ? 0m : request.Value;
        var maxDiscountAmount = type is PromotionType.FreeShipping or PromotionType.Gift ? null : request.MaxDiscountAmount;
        var minQuantity = type == PromotionType.Gift ? request.MinQuantity : null;

        List<GiftItemInput>? giftItems = null;
        if (type == PromotionType.Gift && request.GiftItems is { Count: > 0 })
        {
            var resolveResult = await ResolveGiftItemsAsync(request.GiftItems, cancellationToken);
            if (resolveResult.IsFailure) return Result.Failure<PromotionDto>(resolveResult.Error);
            giftItems = resolveResult.Value;
        }

        var result = promotion.Update(
            request.Name, request.Description, type, value, maxDiscountAmount,
            request.MinOrderSubtotal, request.StartsAt, request.EndsAt, request.UsageLimit, request.PerUserLimit,
            scope, request.TargetProductIds, request.TargetCategoryIds, minQuantity, giftItems);
        if (result.IsFailure) return Result.Failure<PromotionDto>(result.Error);

        _promotions.Update(promotion);
        await _uow.SaveChangesAsync(cancellationToken);

        var coupons = await _coupons.GetByPromotionIdAsync(promotion.Id, cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        return Result.Success(promotion.ToDto(coupons, now));
    }

    /// <summary>Resolves each raw gift input to a concrete variant: an explicit variantId is validated via the catalog; a productId-only input falls back to its single active variant (<see cref="IProductPriceLookup"/>'s convention), else <see cref="PromotionsErrors.GiftVariantRequired"/>.</summary>
    private async Task<Result<List<GiftItemInput>>> ResolveGiftItemsAsync(IReadOnlyList<GiftItemInputDto> items, CancellationToken ct)
    {
        var resolved = new List<GiftItemInput>();
        foreach (var g in items)
        {
            var variantId = g.VariantId;
            if (variantId is null && g.ProductId.HasValue)
            {
                var info = await _priceLookup.GetAsync(g.ProductId.Value, ct);
                variantId = info?.VariantId;
            }

            if (variantId is null) return Result.Failure<List<GiftItemInput>>(PromotionsErrors.GiftVariantRequired);

            var variant = await _catalogReader.GetVariantAsync(variantId.Value, ct);
            if (variant is null) return Result.Failure<List<GiftItemInput>>(PromotionsErrors.GiftProductNotFound);
            if (!variant.IsActive) return Result.Failure<List<GiftItemInput>>(PromotionsErrors.GiftVariantInactive);
            if (variant.ProductStatus != "Published") return Result.Failure<List<GiftItemInput>>(PromotionsErrors.GiftProductNotPublished);

            resolved.Add(new GiftItemInput(variant.ProductId, g.Quantity, variant.VariantId));
        }

        return Result.Success(resolved);
    }
}
