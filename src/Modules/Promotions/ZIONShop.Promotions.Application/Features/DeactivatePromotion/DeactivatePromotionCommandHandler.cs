using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.Promotions.Application.Interfaces;
using ZIONShop.Promotions.Application.Mappings;
using ZIONShop.Promotions.Domain.Exceptions;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.DeactivatePromotion;

public class DeactivatePromotionCommandHandler : IRequestHandler<DeactivatePromotionCommand, Result<PromotionDto>>
{
    private readonly IPromotionRepository _promotions;
    private readonly ICouponCodeRepository _coupons;
    private readonly IPromotionsUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;

    public DeactivatePromotionCommandHandler(IPromotionRepository promotions, ICouponCodeRepository coupons, IPromotionsUnitOfWork uow, TimeProvider timeProvider)
    {
        _promotions = promotions;
        _coupons = coupons;
        _uow = uow;
        _timeProvider = timeProvider;
    }

    public async Task<Result<PromotionDto>> Handle(DeactivatePromotionCommand request, CancellationToken cancellationToken)
    {
        var promotion = await _promotions.GetByIdAsync(request.PromotionId, cancellationToken);
        if (promotion is null) return Result.Failure<PromotionDto>(PromotionsErrors.PromotionNotFound);

        var result = promotion.Deactivate();
        if (result.IsFailure) return Result.Failure<PromotionDto>(result.Error);

        _promotions.Update(promotion);
        await _uow.SaveChangesAsync(cancellationToken);

        var coupons = await _coupons.GetByPromotionIdAsync(promotion.Id, cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        return Result.Success(promotion.ToDto(coupons, now));
    }
}
