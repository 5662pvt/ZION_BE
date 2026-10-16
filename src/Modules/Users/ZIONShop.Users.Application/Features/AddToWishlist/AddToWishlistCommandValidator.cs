using FluentValidation;

namespace ZIONShop.Users.Application.Features.AddToWishlist;

public class AddToWishlistCommandValidator : AbstractValidator<AddToWishlistCommand>
{
    public AddToWishlistCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
    }
}
