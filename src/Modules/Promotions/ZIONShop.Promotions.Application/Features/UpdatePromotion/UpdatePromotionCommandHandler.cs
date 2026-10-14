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

    public UpdatePromotionCommandHandler(IPromotionRepository promotions, ICouponCodeRepository coupons, IPromotionsUnitOfWork uow, TimeProvider timeProvider, IProductCatalogReader catalogReader)
    {
        _promotions = promotions;
        _coupons = coupons;
        _uow = uow;
        _timeProvider = timeProvider;
        _catalogReader = catalogReader;
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
        var giftItems = type == PromotionType.Gift
            ? request.GiftItems?.Select(g => new GiftItemInput(g.ProductId, g.Quantity)).ToList()
            : null;

        if (type == PromotionType.Gift && giftItems is { Count: > 0 })
        {
            foreach (var item in giftItems)
            {
                var product = await _catalogReader.GetAsync(item.ProductId, cancellationToken);
                if (product is null) return Result.Failure<PromotionDto>(PromotionsErrors.GiftProductNotFound);
            }
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
}
