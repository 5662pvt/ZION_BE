using FluentValidation;

namespace ZIONShop.Payments.Application.Features.ListPayments;

public class ListPaymentsQueryValidator : AbstractValidator<ListPaymentsQuery>
{
    public ListPaymentsQueryValidator()
    {
        RuleFor(x => x.From)
            .Must((query, from) => !from.HasValue || !query.To.HasValue || from.Value.Date <= query.To.Value.Date)
            .WithMessage("'from' phải nhỏ hơn hoặc bằng 'to'");
    }
}
