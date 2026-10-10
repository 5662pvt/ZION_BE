using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.DeactivateCoupon;

public record DeactivateCouponCommand(Guid PromotionId, Guid CouponId) : IRequest<Result<CouponDto>>;
