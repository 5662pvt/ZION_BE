using FluentValidation;

namespace ZIONShop.Orders.Application.Features.PlaceOrder;

public class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.AddressId).NotEmpty();
        RuleFor(x => x.PaymentMethod).NotEmpty().Must(m => m is "COD" or "Online")
            .WithMessage("PaymentMethod must be 'COD' or 'Online'.");
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}
