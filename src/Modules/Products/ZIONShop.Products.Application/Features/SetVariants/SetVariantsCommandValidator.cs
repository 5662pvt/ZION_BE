using FluentValidation;

namespace ZIONShop.Products.Application.Features.SetVariants;

public class SetVariantsCommandValidator : AbstractValidator<SetVariantsCommand>
{
    public SetVariantsCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Options).Must(o => o.Count <= 2).WithMessage("A product supports at most 2 option tiers");
        RuleFor(x => x.Variants).NotEmpty();

        RuleForEach(x => x.Options).ChildRules(option =>
        {
            option.RuleFor(o => o.Name).NotEmpty().MaximumLength(30);
            option.RuleFor(o => o.Values).NotEmpty();
        });

        RuleForEach(x => x.Variants).ChildRules(variant =>
        {
            variant.RuleFor(v => v.Sku).NotEmpty().Length(3, 64);
            variant.RuleFor(v => v.Price).GreaterThanOrEqualTo(0);
            variant.RuleFor(v => v.Currency).NotEmpty().Length(3);
        });
    }
}
