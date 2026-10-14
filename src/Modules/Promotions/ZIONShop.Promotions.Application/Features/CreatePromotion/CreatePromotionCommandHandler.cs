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

namespace ZIONShop.Promotions.Application.Features.CreatePromotion;

public class CreatePromotionCommandHandler : IRequestHandler<CreatePromotionCommand, Result<PromotionDto>>
{
    private readonly IPromotionRepository _promotions;
    private readonly IPromotionsUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;
    private readonly IProductCatalogReader _catalogReader;

    public CreatePromotionCommandHandler(IPromotionRepository promotions, IPromotionsUnitOfWork uow, TimeProvider timeProvider, IProductCatalogReader catalogReader)
    {
        _promotions = promotions;
        _uow = uow;
        _timeProvider = timeProvider;
        _catalogReader = catalogReader;
    }

    public async Task<Result<PromotionDto>> Handle(CreatePromotionCommand request, CancellationToken cancellationToken)
    {
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

        var createResult = Promotion.Create(
            request.Name, request.Description, type, value, maxDiscountAmount,
            request.MinOrderSubtotal, request.StartsAt, request.EndsAt, request.UsageLimit, request.PerUserLimit,
            scope, request.TargetProductIds, request.TargetCategoryIds, minQuantity, giftItems);
        if (createResult.IsFailure) return Result.Failure<PromotionDto>(createResult.Error);

        var promotion = createResult.Value;
        await _promotions.AddAsync(promotion, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        return Result.Success(promotion.ToDto(Array.Empty<CouponCode>(), now));
    }
}
