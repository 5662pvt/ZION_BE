using FluentValidation;

namespace ZIONShop.Loyalty.Application.Features.AdjustLoyaltyPoints;

public class AdjustLoyaltyPointsCommandValidator : AbstractValidator<AdjustLoyaltyPointsCommand>
{
    public AdjustLoyaltyPointsCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Delta).NotEqual(0).InclusiveBetween(-1_000_000, 1_000_000);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
