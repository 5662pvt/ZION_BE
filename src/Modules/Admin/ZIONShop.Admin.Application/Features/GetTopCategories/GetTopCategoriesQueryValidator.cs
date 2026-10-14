using FluentValidation;

namespace ZIONShop.Admin.Application.Features.GetTopCategories;

public class GetTopCategoriesQueryValidator : AbstractValidator<GetTopCategoriesQuery>
{
    public GetTopCategoriesQueryValidator()
    {
        RuleFor(x => x.SortBy)
            .Must(s => s is null || s.Equals("revenue", StringComparison.OrdinalIgnoreCase) || s.Equals("quantity", StringComparison.OrdinalIgnoreCase))
            .WithMessage("sortBy chỉ nhận 'revenue' hoặc 'quantity'");
    }
}
