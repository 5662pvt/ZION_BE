using FluentValidation;

namespace ZIONShop.Users.Application.Features.RemoveFromWishlist;

public class RemoveFromWishlistCommandValidator : AbstractValidator<RemoveFromWishlistCommand>
{
    public RemoveFromWishlistCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
    }
}
