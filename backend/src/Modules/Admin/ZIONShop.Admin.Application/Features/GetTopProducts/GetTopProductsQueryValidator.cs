using FluentValidation;

namespace ZIONShop.Admin.Application.Features.GetTopProducts;

public class GetTopProductsQueryValidator : AbstractValidator<GetTopProductsQuery>
{
    public GetTopProductsQueryValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, 50).WithMessage("Số lượng phải từ 1 đến 50");
        RuleFor(x => x.SortBy)
            .Must(s => s is null || s.Equals("revenue", StringComparison.OrdinalIgnoreCase) || s.Equals("quantity", StringComparison.OrdinalIgnoreCase))
            .WithMessage("sortBy chỉ nhận 'revenue' hoặc 'quantity'");
    }
}
