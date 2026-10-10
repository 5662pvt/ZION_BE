using MediatR;
using ZIONShop.Contracts.Promotions;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.ValidateCoupon;

public class ValidateCouponCommandHandler : IRequestHandler<ValidateCouponCommand, Result<CouponValidationResultDto>>
{
    private readonly IPromotionEvaluator _evaluator;

    public ValidateCouponCommandHandler(IPromotionEvaluator evaluator) => _evaluator = evaluator;

    public async Task<Result<CouponValidationResultDto>> Handle(ValidateCouponCommand request, CancellationToken cancellationToken)
    {
        var (quote, error) = await _evaluator.QuoteAsync(request.Code, request.UserId, request.Subtotal, cancellationToken);
        if (error is not null)
            return Result.Failure<CouponValidationResultDto>(Error.Validation(error.Code, error.Message));

        return Result.Success(new CouponValidationResultDto(quote!.Code, quote.PromotionName, quote.DiscountAmount));
    }
}
