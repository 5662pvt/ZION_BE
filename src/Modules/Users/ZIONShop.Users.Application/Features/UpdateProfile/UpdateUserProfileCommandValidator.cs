using FluentValidation;

namespace ZIONShop.Users.Application.Features.UpdateProfile;

public class UpdateUserProfileCommandValidator : AbstractValidator<UpdateUserProfileCommand>
{
    public UpdateUserProfileCommandValidator()
    {
        RuleFor(x => x.FullName).MaximumLength(128);
        RuleFor(x => x.PhoneNumber).MaximumLength(32).Matches("^[+0-9 ()-]*$").When(x => !string.IsNullOrEmpty(x.PhoneNumber));
    }
}
