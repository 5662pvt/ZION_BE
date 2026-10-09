using FluentValidation;
using ZIONShop.Users.Application.Validation;

namespace ZIONShop.Users.Application.Features.UpdateAddress;

public class UpdateAddressCommandValidator : AbstractValidator<UpdateAddressCommand>
{
    public UpdateAddressCommandValidator()
    {
        RuleFor(x => x.AddressId).NotEmpty();
        RuleFor(x => x.RecipientName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(15).Must(VietnamesePhoneNumber.IsValid)
            .WithMessage("Phone number must be a valid Vietnamese mobile or landline number");
        RuleFor(x => x.Province).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Ward).NotEmpty().MaximumLength(100);
        RuleFor(x => x.StreetAddress).NotEmpty().MaximumLength(255);
    }
}
