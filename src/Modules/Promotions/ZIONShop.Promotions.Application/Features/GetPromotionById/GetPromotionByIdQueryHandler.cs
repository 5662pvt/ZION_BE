using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.Promotions.Application.Mappings;
using ZIONShop.Promotions.Domain.Exceptions;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.GetPromotionById;

public class GetPromotionByIdQueryHandler : IRequestHandler<GetPromotionByIdQuery, Result<PromotionDto>>
{
    private readonly IPromotionRepository _promotions;
    private readonly ICouponCodeRepository _coupons;
    private readonly TimeProvider _timeProvider;

    public GetPromotionByIdQueryHandler(IPromotionRepository promotions, ICouponCodeRepository coupons, TimeProvider timeProvider)
    {
        _promotions = promotions;
        _coupons = coupons;
        _timeProvider = timeProvider;
    }

    public async Task<Result<PromotionDto>> Handle(GetPromotionByIdQuery request, CancellationToken cancellationToken)
    {
        var promotion = await _promotions.GetByIdAsync(request.PromotionId, cancellationToken);
        if (promotion is null) return Result.Failure<PromotionDto>(PromotionsErrors.PromotionNotFound);

        var coupons = await _coupons.GetByPromotionIdAsync(promotion.Id, cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        return Result.Success(promotion.ToDto(coupons, now));
    }
}
