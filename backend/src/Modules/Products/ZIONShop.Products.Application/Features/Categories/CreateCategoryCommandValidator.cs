using FluentValidation;

namespace ZIONShop.Products.Application.Features.Categories;

public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(128).Matches("^[a-z0-9-]+$");
    }
}
