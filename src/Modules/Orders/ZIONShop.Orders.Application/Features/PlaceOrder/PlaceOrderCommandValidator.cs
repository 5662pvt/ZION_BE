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
        RuleFor(x => x.CouponCode).MaximumLength(30);
        // Idempotency-Key format (1..100 chars + charset) is enforced in the handler, not here: ValidationBehavior
        // always prefixes a FluentValidation failure code with the property name ("IdempotencyKey.<code>"), which
        // cannot produce the contract-exact "Orders.InvalidIdempotencyKey" code. Same reasoning as PaymentMethod
        // above, whose shape is loosely checked here but whose authoritative "Orders.InvalidPaymentMethod" error
        // is raised by the handler.
    }
}
