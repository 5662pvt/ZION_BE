using FluentValidation;

namespace ZIONShop.Promotions.Application.Features.UpdateFlashSale;

public class UpdateFlashSaleCommandValidator : AbstractValidator<UpdateFlashSaleCommand>
{
    public UpdateFlashSaleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt).WithMessage("'endsAt' phải lớn hơn 'startsAt'");
    }
}
