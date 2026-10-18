using FluentValidation;

namespace ZIONShop.Reviews.Application.Features.AskProductQuestion;

public class AskProductQuestionCommandValidator : AbstractValidator<AskProductQuestionCommand>
{
    public AskProductQuestionCommandValidator()
    {
        RuleFor(x => x.Content).NotNull()
            .Must(c => c.Trim().Length is >= 5 and <= 1000)
            .WithMessage("'content' phải có độ dài 5-1000 ký tự");
    }
}
