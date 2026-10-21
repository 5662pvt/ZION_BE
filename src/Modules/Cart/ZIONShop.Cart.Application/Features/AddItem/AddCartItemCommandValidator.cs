using FluentValidation;

namespace ZIONShop.Cart.Application.Features.AddItem;

public class AddCartItemCommandValidator : AbstractValidator<AddCartItemCommand>
{
    public AddCartItemCommandValidator()
    {
        RuleFor(x => x).Must(x => x.ProductId.HasValue || x.VariantId.HasValue)
            .WithMessage("Either ProductId or VariantId must be provided.");
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(100);
        RuleFor(x => x).Must(x => x.UserId.HasValue || !string.IsNullOrWhiteSpace(x.AnonymousId))
            .WithMessage("Either UserId or AnonymousId must be provided.");
    }
}
