using FluentValidation;

namespace ZIONShop.Users.Application.Features.AddAddress;

public class AddAddressCommandValidator : AbstractValidator<AddAddressCommand>
{
    public AddAddressCommandValidator()
    {
        RuleFor(x => x.Line1).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Line2).MaximumLength(256);
        RuleFor(x => x.City).NotEmpty().MaximumLength(128);
        RuleFor(x => x.State).MaximumLength(128);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(64);
        RuleFor(x => x.PostalCode).NotEmpty().MaximumLength(16);
    }
}
