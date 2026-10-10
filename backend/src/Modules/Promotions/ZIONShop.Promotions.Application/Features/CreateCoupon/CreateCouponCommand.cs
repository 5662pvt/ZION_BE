using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.CreateCoupon;

public record CreateCouponCommand(Guid PromotionId, string Code) : IRequest<Result<CouponDto>>;
