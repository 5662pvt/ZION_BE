using FluentValidation;

namespace ZIONShop.Users.Application.Features.RemoveAddress;

public class RemoveAddressCommandValidator : AbstractValidator<RemoveAddressCommand>
{
    public RemoveAddressCommandValidator()
    {
        RuleFor(x => x.AddressId).NotEmpty();
    }
}
