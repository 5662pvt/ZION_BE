using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.ValidateCoupon;

public record ValidateCouponCommand(Guid UserId, string Code) : IRequest<Result<CouponValidationResultDto>>;
