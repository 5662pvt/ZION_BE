using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.Promotions.Application.Interfaces;
using ZIONShop.Promotions.Application.Mappings;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Enums;
using ZIONShop.Promotions.Domain.Exceptions;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.CreateCoupon;

public class CreateCouponCommandHandler : IRequestHandler<CreateCouponCommand, Result<CouponDto>>
{
    private readonly IPromotionRepository _promotions;
    private readonly ICouponCodeRepository _coupons;
    private readonly IPromotionsUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;

    public CreateCouponCommandHandler(IPromotionRepository promotions, ICouponCodeRepository coupons, IPromotionsUnitOfWork uow, TimeProvider timeProvider)
    {
        _promotions = promotions;
        _coupons = coupons;
        _uow = uow;
        _timeProvider = timeProvider;
    }

    public async Task<Result<CouponDto>> Handle(CreateCouponCommand request, CancellationToken cancellationToken)
    {
        var promotion = await _promotions.GetByIdAsync(request.PromotionId, cancellationToken);
        if (promotion is null) return Result.Failure<CouponDto>(PromotionsErrors.PromotionNotFound);

        if (promotion.Type == PromotionType.Gift)
            return Result.Failure<CouponDto>(PromotionsErrors.CouponNotAllowedForGift);

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        if (await _coupons.ExistsByCodeAsync(normalizedCode, cancellationToken))
            return Result.Failure<CouponDto>(PromotionsErrors.CouponCodeTaken);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var coupon = CouponCode.Create(promotion.Id, normalizedCode, now);

        await _coupons.AddAsync(coupon, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success(coupon.ToDto());
    }
}
