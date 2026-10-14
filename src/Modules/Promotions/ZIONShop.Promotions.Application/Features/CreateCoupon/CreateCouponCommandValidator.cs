using FluentValidation;

namespace ZIONShop.Promotions.Application.Features.CreateCoupon;

public class CreateCouponCommandValidator : AbstractValidator<CreateCouponCommand>
{
    public CreateCouponCommandValidator()
    {
        RuleFor(x => x.PromotionId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().Length(3, 30)
            .Matches("^[A-Za-z0-9-]+$").WithMessage("'code' chỉ chứa A-Z, 0-9, '-'");
    }
}
