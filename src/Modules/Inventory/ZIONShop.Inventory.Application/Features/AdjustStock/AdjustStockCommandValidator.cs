using FluentValidation;

namespace ZIONShop.Inventory.Application.Features.AdjustStock;

public class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Delta).NotEqual(0).WithMessage("Delta must not be zero");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(256);
    }
}
