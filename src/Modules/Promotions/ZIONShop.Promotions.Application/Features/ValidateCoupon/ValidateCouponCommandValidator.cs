using FluentValidation;

namespace ZIONShop.Promotions.Application.Features.ValidateCoupon;

public class ValidateCouponCommandValidator : AbstractValidator<ValidateCouponCommand>
{
    public ValidateCouponCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty();
    }
}
