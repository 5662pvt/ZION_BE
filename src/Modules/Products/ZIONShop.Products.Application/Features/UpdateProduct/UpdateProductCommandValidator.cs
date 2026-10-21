using FluentValidation;

namespace ZIONShop.Products.Application.Features.UpdateProduct;

public class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Brand).MaximumLength(128);

        When(x => x.Price.HasValue, () =>
        {
            RuleFor(x => x.Price!.Value).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Currency).NotEmpty().Length(3);
        });
    }
}
