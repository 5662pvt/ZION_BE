using FluentValidation;

namespace ZIONShop.Reviews.Application.Features.AnswerProductQuestion;

public class AnswerProductQuestionCommandValidator : AbstractValidator<AnswerProductQuestionCommand>
{
    public AnswerProductQuestionCommandValidator()
    {
        RuleFor(x => x.Content).NotEmpty().MaximumLength(2000);
    }
}
