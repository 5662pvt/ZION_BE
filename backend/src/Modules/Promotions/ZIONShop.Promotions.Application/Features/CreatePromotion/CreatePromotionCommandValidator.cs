using FluentValidation;

namespace ZIONShop.Promotions.Application.Features.CreatePromotion;

public class CreatePromotionCommandValidator : AbstractValidator<CreatePromotionCommand>
{
    public CreatePromotionCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Type).NotEmpty().Must(t => t is "PercentageOff" or "FixedAmountOff" or "FreeShipping")
            .WithMessage("'type' phải là 'PercentageOff', 'FixedAmountOff' hoặc 'FreeShipping'");
        RuleFor(x => x.MinOrderSubtotal).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt).WithMessage("'endsAt' phải lớn hơn 'startsAt'");
        RuleFor(x => x.UsageLimit).GreaterThanOrEqualTo(1).When(x => x.UsageLimit.HasValue);
        RuleFor(x => x.PerUserLimit).GreaterThanOrEqualTo(1).When(x => x.PerUserLimit.HasValue);

        RuleFor(x => x.Value).InclusiveBetween(1m, 100m).When(x => x.Type == "PercentageOff")
            .WithMessage("'value' phải trong khoảng 1-100 với loại PercentageOff");
        RuleFor(x => x.MaxDiscountAmount).GreaterThan(0m).When(x => x.Type == "PercentageOff" && x.MaxDiscountAmount.HasValue);

        RuleFor(x => x.Value).GreaterThan(0m).When(x => x.Type == "FixedAmountOff")
            .WithMessage("'value' phải lớn hơn 0 với loại FixedAmountOff");

        RuleFor(x => x.Scope).NotEmpty().Must(s => s is "Order" or "Products" or "Categories")
            .WithMessage("'scope' phải là 'Order', 'Products' hoặc 'Categories'");

        RuleFor(x => x.TargetProductIds).Must(ids => ids is { Count: > 0 }).When(x => x.Scope == "Products")
            .WithMessage("'targetProductIds' không được để trống khi scope là 'Products'");
        RuleFor(x => x.TargetProductIds).Must(ids => ids is null or { Count: 0 }).When(x => x.Scope != "Products")
            .WithMessage("'targetProductIds' chỉ áp dụng khi scope là 'Products'");

        RuleFor(x => x.TargetCategoryIds).Must(ids => ids is { Count: > 0 }).When(x => x.Scope == "Categories")
            .WithMessage("'targetCategoryIds' không được để trống khi scope là 'Categories'");
        RuleFor(x => x.TargetCategoryIds).Must(ids => ids is null or { Count: 0 }).When(x => x.Scope != "Categories")
            .WithMessage("'targetCategoryIds' chỉ áp dụng khi scope là 'Categories'");
    }
}
