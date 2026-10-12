using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.Promotions.Application.Interfaces;
using ZIONShop.Promotions.Application.Mappings;
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

    public UpdatePromotionCommandHandler(IPromotionRepository promotions, ICouponCodeRepository coupons, IPromotionsUnitOfWork uow, TimeProvider timeProvider)
    {
        _promotions = promotions;
        _coupons = coupons;
        _uow = uow;
        _timeProvider = timeProvider;
    }

    public async Task<Result<PromotionDto>> Handle(UpdatePromotionCommand request, CancellationToken cancellationToken)
    {
        var promotion = await _promotions.GetByIdAsync(request.PromotionId, cancellationToken);
        if (promotion is null) return Result.Failure<PromotionDto>(PromotionsErrors.PromotionNotFound);

        if (!Enum.TryParse<PromotionType>(request.Type, ignoreCase: true, out var type))
            return Result.Failure<PromotionDto>(PromotionsErrors.InvalidPromotionType);

        if (!Enum.TryParse<PromotionScope>(request.Scope, ignoreCase: true, out var scope))
            return Result.Failure<PromotionDto>(PromotionsErrors.InvalidPromotionScope);

        var value = type == PromotionType.FreeShipping ? 0m : request.Value;
        var maxDiscountAmount = type == PromotionType.FreeShipping ? null : request.MaxDiscountAmount;

        var result = promotion.Update(
            request.Name, request.Description, type, value, maxDiscountAmount,
            request.MinOrderSubtotal, request.StartsAt, request.EndsAt, request.UsageLimit, request.PerUserLimit,
            scope, request.TargetProductIds, request.TargetCategoryIds);
        if (result.IsFailure) return Result.Failure<PromotionDto>(result.Error);

        _promotions.Update(promotion);
        await _uow.SaveChangesAsync(cancellationToken);

        var coupons = await _coupons.GetByPromotionIdAsync(promotion.Id, cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        return Result.Success(promotion.ToDto(coupons, now));
    }
}
