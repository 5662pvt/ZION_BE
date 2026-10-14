using FluentValidation;

namespace ZIONShop.Orders.Application.Features.SearchOrders;

public class SearchOrdersQueryValidator : AbstractValidator<SearchOrdersQuery>
{
    public SearchOrdersQueryValidator()
    {
        RuleFor(x => x.From)
            .Must((query, from) => !from.HasValue || !query.To.HasValue || from.Value.Date <= query.To.Value.Date)
            .WithMessage("'from' phải nhỏ hơn hoặc bằng 'to'");
    }
}
