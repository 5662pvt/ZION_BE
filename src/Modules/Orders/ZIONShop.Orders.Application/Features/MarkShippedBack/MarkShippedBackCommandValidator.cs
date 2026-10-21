using FluentValidation;

namespace ZIONShop.Orders.Application.Features.MarkShippedBack;

public class MarkShippedBackCommandValidator : AbstractValidator<MarkShippedBackCommand>
{
    public MarkShippedBackCommandValidator()
    {
        RuleFor(x => x.ReturnRequestId).NotEmpty();
        RuleFor(x => x.TrackingCode).MaximumLength(100);
    }
}
