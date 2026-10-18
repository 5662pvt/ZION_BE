using FluentValidation;

namespace ZIONShop.Promotions.Application.Features.AddFlashSaleItem;

public class AddFlashSaleItemCommandValidator : AbstractValidator<AddFlashSaleItemCommand>
{
    public AddFlashSaleItemCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.FlashPrice).GreaterThan(0m);
        RuleFor(x => x.AllocatedQuantity).GreaterThan(0);
    }
}
