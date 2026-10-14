using FluentValidation;

namespace ZIONShop.Reviews.Application.Features.GetReviewSummaries;

public class GetReviewSummariesQueryValidator : AbstractValidator<GetReviewSummariesQuery>
{
    public GetReviewSummariesQueryValidator()
    {
        RuleFor(x => x.ProductIds).Must(ids => ids.Count <= 100)
            .WithMessage("Tối đa 100 productId mỗi lần");
    }
}
