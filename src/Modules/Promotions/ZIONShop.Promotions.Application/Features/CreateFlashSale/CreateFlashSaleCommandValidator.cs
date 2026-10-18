using FluentValidation;

namespace ZIONShop.Promotions.Application.Features.CreateFlashSale;

public class CreateFlashSaleCommandValidator : AbstractValidator<CreateFlashSaleCommand>
{
    public CreateFlashSaleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt).WithMessage("'endsAt' phải lớn hơn 'startsAt'");
    }
}
