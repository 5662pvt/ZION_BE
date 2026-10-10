using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.Promotions.Application.Interfaces;
using ZIONShop.Promotions.Application.Mappings;
using ZIONShop.Promotions.Domain.Exceptions;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.DeactivateCoupon;

public class DeactivateCouponCommandHandler : IRequestHandler<DeactivateCouponCommand, Result<CouponDto>>
{
    private readonly ICouponCodeRepository _coupons;
    private readonly IPromotionsUnitOfWork _uow;

    public DeactivateCouponCommandHandler(ICouponCodeRepository coupons, IPromotionsUnitOfWork uow)
    {
        _coupons = coupons;
        _uow = uow;
    }

    public async Task<Result<CouponDto>> Handle(DeactivateCouponCommand request, CancellationToken cancellationToken)
    {
        var coupon = await _coupons.GetByIdAsync(request.CouponId, cancellationToken);
        if (coupon is null || coupon.PromotionId != request.PromotionId)
            return Result.Failure<CouponDto>(PromotionsErrors.CouponNotFound);

        var result = coupon.Deactivate();
        if (result.IsFailure) return Result.Failure<CouponDto>(result.Error);

        _coupons.Update(coupon);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success(coupon.ToDto());
    }
}
