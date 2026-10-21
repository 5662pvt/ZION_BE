using FluentValidation;

namespace ZIONShop.Promotions.Application.Features.AddFlashSaleItem;

public class AddFlashSaleItemCommandValidator : AbstractValidator<AddFlashSaleItemCommand>
{
    public AddFlashSaleItemCommandValidator()
    {
        RuleFor(x => x).Must(x => x.ProductId.HasValue || x.VariantId.HasValue)
            .WithMessage("Phải cung cấp 'productId' hoặc 'variantId'");
        RuleFor(x => x.FlashPrice).GreaterThan(0m);
        RuleFor(x => x.AllocatedQuantity).GreaterThan(0);
    }
}
