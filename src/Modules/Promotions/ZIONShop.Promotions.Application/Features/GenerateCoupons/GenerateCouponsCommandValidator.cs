using FluentValidation;

namespace ZIONShop.Promotions.Application.Features.GenerateCoupons;

public class GenerateCouponsCommandValidator : AbstractValidator<GenerateCouponsCommand>
{
    public GenerateCouponsCommandValidator()
    {
        RuleFor(x => x.PromotionId).NotEmpty();
        RuleFor(x => x.Prefix).NotEmpty().MaximumLength(10)
            .Matches("^[A-Za-z0-9-]+$").WithMessage("'prefix' chỉ chứa A-Z, 0-9, '-'");
        RuleFor(x => x.Count).InclusiveBetween(1, 500);
    }
}
