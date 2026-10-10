using FluentValidation;

namespace ZIONShop.Promotions.Application.Features.UpdatePromotion;

public class UpdatePromotionCommandValidator : AbstractValidator<UpdatePromotionCommand>
{
    public UpdatePromotionCommandValidator()
    {
        RuleFor(x => x.PromotionId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Type).NotEmpty().Must(t => t is "PercentageOff" or "FixedAmountOff")
            .WithMessage("'type' phải là 'PercentageOff' hoặc 'FixedAmountOff'");
        RuleFor(x => x.MinOrderSubtotal).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt).WithMessage("'endsAt' phải lớn hơn 'startsAt'");
        RuleFor(x => x.UsageLimit).GreaterThanOrEqualTo(1).When(x => x.UsageLimit.HasValue);
        RuleFor(x => x.PerUserLimit).GreaterThanOrEqualTo(1).When(x => x.PerUserLimit.HasValue);

        RuleFor(x => x.Value).InclusiveBetween(1m, 100m).When(x => x.Type == "PercentageOff")
            .WithMessage("'value' phải trong khoảng 1-100 với loại PercentageOff");
        RuleFor(x => x.MaxDiscountAmount).GreaterThan(0m).When(x => x.Type == "PercentageOff" && x.MaxDiscountAmount.HasValue);

        RuleFor(x => x.Value).GreaterThan(0m).When(x => x.Type == "FixedAmountOff")
            .WithMessage("'value' phải lớn hơn 0 với loại FixedAmountOff");
    }
}
