using FluentValidation;

namespace ZIONShop.Promotions.Application.Features.UpdatePromotion;

public class UpdatePromotionCommandValidator : AbstractValidator<UpdatePromotionCommand>
{
    public UpdatePromotionCommandValidator()
    {
        RuleFor(x => x.PromotionId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Type).NotEmpty().Must(t => t is "PercentageOff" or "FixedAmountOff" or "FreeShipping" or "Gift")
            .WithMessage("'type' phải là 'PercentageOff', 'FixedAmountOff', 'FreeShipping' hoặc 'Gift'");
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

        RuleFor(x => x.GiftItems).Must(items => items is { Count: > 0 and <= 5 }).When(x => x.Type == "Gift")
            .WithMessage("'giftItems' phải có 1-5 sản phẩm khi type là 'Gift'");
        RuleFor(x => x.GiftItems).Must(items => items is null or { Count: 0 }).When(x => x.Type != "Gift")
            .WithMessage("'giftItems' chỉ áp dụng khi type là 'Gift'");
        RuleFor(x => x.GiftItems)
            .Must(items => items!.Select(i => i.VariantId ?? i.ProductId).Distinct().Count() == items!.Count)
            .When(x => x.Type == "Gift" && x.GiftItems is { Count: > 0 })
            .WithMessage("'giftItems' không được trùng sản phẩm");
        RuleForEach(x => x.GiftItems).ChildRules(item =>
        {
            item.RuleFor(i => i).Must(i => i.ProductId.HasValue || i.VariantId.HasValue)
                .WithMessage("Mỗi quà tặng phải có 'productId' hoặc 'variantId'");
            item.RuleFor(i => i.Quantity).InclusiveBetween(1, 10);
        }).When(x => x.GiftItems is { Count: > 0 });

        RuleFor(x => x.MinQuantity).GreaterThanOrEqualTo(1).When(x => x.MinQuantity.HasValue);
        RuleFor(x => x.MinQuantity).NotNull().When(x => x.Type == "Gift" && x.Scope != "Order")
            .WithMessage("'minQuantity' là bắt buộc khi scope khác 'Order' với khuyến mãi quà tặng");
        RuleFor(x => x.MinQuantity).Null().When(x => x.Type == "Gift" && x.Scope == "Order")
            .WithMessage("'minQuantity' phải để trống khi scope là 'Order'");
        RuleFor(x => x.MinOrderSubtotal).GreaterThan(0m).When(x => x.Type == "Gift" && x.Scope == "Order")
            .WithMessage("'minOrderSubtotal' phải lớn hơn 0 khi scope là 'Order' với khuyến mãi quà tặng");
    }
}
