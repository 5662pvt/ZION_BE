using FluentValidation;

namespace ZIONShop.Orders.Application.Features.RequestReturn;

public class RequestReturnCommandValidator : AbstractValidator<RequestReturnCommand>
{
    public RequestReturnCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}
