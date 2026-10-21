using FluentValidation;

namespace ZIONShop.Products.Application.Features.CreateProduct;

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(256).Matches("^[a-z0-9-]+$");
        RuleFor(x => x.Brand).MaximumLength(128);

        When(x => x.Options is null && x.Variants is null, () =>
        {
            RuleFor(x => x.Sku).NotEmpty().Length(3, 64);
            RuleFor(x => x.Price).NotNull().GreaterThanOrEqualTo(0);
            RuleFor(x => x.Currency).NotEmpty().Length(3);
        });

        When(x => x.Options is not null || x.Variants is not null, () =>
        {
            RuleFor(x => x.Variants).NotEmpty();
            RuleFor(x => x.Variants).Must(v => v is null || v.Count <= 400).WithMessage("A product supports at most 400 variants");

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
        });
    }
}
