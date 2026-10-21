using FluentValidation;

namespace ZIONShop.Products.Application.Features.SetVariants;

public class SetVariantsCommandValidator : AbstractValidator<SetVariantsCommand>
{
    public SetVariantsCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Options).Must(o => o.Count <= 2).WithMessage("A product supports at most 2 option tiers");
        RuleFor(x => x.Variants).NotEmpty();
        RuleFor(x => x.Variants).Must(v => v.Count <= 400).WithMessage("A product supports at most 400 variants");

        RuleForEach(x => x.Options).ChildRules(option =>
        {
            option.RuleFor(o => o.Name).NotEmpty().MaximumLength(30);
            option.RuleFor(o => o.Values).NotEmpty();
            option.RuleFor(o => o.Values).Must(v => v.Count <= 20).WithMessage("Each option supports at most 20 values");
        });

        RuleForEach(x => x.Variants).ChildRules(variant =>
        {
            variant.RuleFor(v => v.Sku).NotEmpty().Length(3, 64);
            variant.RuleFor(v => v.Price).GreaterThanOrEqualTo(0);
            variant.RuleFor(v => v.Currency).NotEmpty().Length(3);
        });
    }
}
