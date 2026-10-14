using FluentValidation;

namespace ZIONShop.Products.Application.Features.CreateProduct;

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(256).Matches("^[a-z0-9-]+$");
        RuleFor(x => x.Sku).NotEmpty().Length(3, 64);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Brand).MaximumLength(128);
    }
}
