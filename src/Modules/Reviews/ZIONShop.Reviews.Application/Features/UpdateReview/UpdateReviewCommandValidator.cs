using FluentValidation;

namespace ZIONShop.Reviews.Application.Features.UpdateReview;

public class UpdateReviewCommandValidator : AbstractValidator<UpdateReviewCommand>
{
    public UpdateReviewCommandValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Title).MaximumLength(120);
        RuleFor(x => x.Content).NotNull()
            .Must(c => c.Trim().Length is >= 10 and <= 2000)
            .WithMessage("'content' phải có độ dài 10-2000 ký tự");
    }
}
