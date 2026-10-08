using FluentValidation;

namespace ZIONShop.Products.Application.Features.AddProductImage;

public class AddProductImageCommandValidator : AbstractValidator<AddProductImageCommand>
{
    private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png", "image/webp" };

    public AddProductImageCommandValidator()
    {
        RuleFor(x => x.ContentType).Must(ct => AllowedContentTypes.Contains(ct))
            .WithMessage("Content type must be image/jpeg, image/png or image/webp");
        RuleFor(x => x.Length).InclusiveBetween(1, 5 * 1024 * 1024)
            .WithMessage("Image size must be between 1 byte and 5MB");
        RuleFor(x => x.Alt).MaximumLength(200);
    }
}
