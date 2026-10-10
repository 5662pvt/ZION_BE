using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.GenerateCoupons;

public record GenerateCouponsCommand(Guid PromotionId, string Prefix, int Count) : IRequest<Result<IReadOnlyList<CouponDto>>>;
