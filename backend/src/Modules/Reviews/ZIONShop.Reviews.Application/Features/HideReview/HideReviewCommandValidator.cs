using FluentValidation;

namespace ZIONShop.Reviews.Application.Features.HideReview;

public class HideReviewCommandValidator : AbstractValidator<HideReviewCommand>
{
    public HideReviewCommandValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(200);
    }
}
