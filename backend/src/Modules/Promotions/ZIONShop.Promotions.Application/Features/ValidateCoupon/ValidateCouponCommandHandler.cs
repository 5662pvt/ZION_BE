using MediatR;
using ZIONShop.Contracts.Cart;
using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Promotions;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.Promotions.Domain.Exceptions;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.ValidateCoupon;

public class ValidateCouponCommandHandler : IRequestHandler<ValidateCouponCommand, Result<CouponValidationResultDto>>
{
    private readonly IPromotionEvaluator _evaluator;
    private readonly ICartReader _cartReader;
    private readonly IShippingFeeCalculator _shippingFeeCalculator;

    public ValidateCouponCommandHandler(IPromotionEvaluator evaluator, ICartReader cartReader, IShippingFeeCalculator shippingFeeCalculator)
    {
        _evaluator = evaluator;
        _cartReader = cartReader;
        _shippingFeeCalculator = shippingFeeCalculator;
    }

    public async Task<Result<CouponValidationResultDto>> Handle(ValidateCouponCommand request, CancellationToken cancellationToken)
    {
        var cart = await _cartReader.GetForUserAsync(request.UserId, cancellationToken);
        if (cart is null || cart.Lines.Count == 0)
            return Result.Failure<CouponValidationResultDto>(PromotionsErrors.CartEmpty);

        var lines = cart.Lines.Select(l => new CouponLine(l.ProductId, l.Quantity, l.UnitPrice * l.Quantity)).ToList();
        var subtotal = lines.Sum(l => l.LineTotal);
        var shippingFee = _shippingFeeCalculator.Calculate(subtotal);

        var couponRequest = new CouponRequest(request.Code, request.UserId, lines, subtotal, shippingFee);
        var (quote, error) = await _evaluator.QuoteAsync(couponRequest, cancellationToken);
        if (error is not null)
            return Result.Failure<CouponValidationResultDto>(Error.Validation(error.Code, error.Message));

        return Result.Success(new CouponValidationResultDto(quote!.Code, quote.PromotionName, quote.DiscountAmount, quote.DiscountTarget));
    }
}
