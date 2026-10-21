using MediatR;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.Products.Application.Interfaces;
using ZIONShop.Products.Application.Mappings;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.Exceptions;
using ZIONShop.Products.Domain.Repositories;
using ZIONShop.Products.Domain.ValueObjects;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.CreateProduct;

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Result<ProductDto>>
{
    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;
    private readonly IProductsUnitOfWork _uow;

    public CreateProductCommandHandler(IProductRepository products, ICategoryRepository categories, IProductsUnitOfWork uow)
    {
        _products = products;
        _categories = categories;
        _uow = uow;
    }

    public async Task<Result<ProductDto>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        if (await _products.SlugExistsAsync(request.Slug.Trim().ToLowerInvariant(), cancellationToken))
            return Result.Failure<ProductDto>(ProductsErrors.SlugAlreadyExists);

        Category? category = null;
        if (request.CategoryId.HasValue)
        {
            category = await _categories.GetByIdAsync(request.CategoryId.Value, cancellationToken);
            if (category is null) return Result.Failure<ProductDto>(ProductsErrors.CategoryNotFound);
        }

        Product product;
        if (request.Options is not null || request.Variants is not null)
        {
            product = Product.CreateDraft(request.Name, request.Slug, request.CategoryId, request.Description, request.Brand);

            if (request.Images is not null)
                foreach (var img in request.Images)
                    product.AddImage(img.Url, img.Alt, img.DisplayOrder);

            foreach (var variant in request.Variants!)
            {
                if (await _products.SkuExistsAsync(variant.Sku.Trim().ToUpperInvariant(), null, cancellationToken))
                    return Result.Failure<ProductDto>(ProductsErrors.SkuAlreadyExists);
            }

            var optionInputs = (request.Options ?? Array.Empty<CreateProductOptionInput>())
                .Select(o => new ProductOptionInput(o.Name, o.Values))
                .ToList();
            var variantInputs = request.Variants!
                .Select(v => new ProductVariantInput(v.Id, v.Option1Value, v.Option2Value, v.Sku, v.Price, v.Currency, v.IsActive, v.ImageId))
                .ToList();

            var setResult = product.SetVariants(optionInputs, variantInputs);
            if (setResult.IsFailure) return Result.Failure<ProductDto>(setResult.Error);

            product.MarkCreated();
        }
        else
        {
            if (await _products.SkuExistsAsync(request.Sku!.Trim().ToUpperInvariant(), null, cancellationToken))
                return Result.Failure<ProductDto>(ProductsErrors.SkuAlreadyExists);

            product = Product.Create(
                request.Name,
                request.Slug,
                Sku.Create(request.Sku!),
                Money.Create(request.Price!.Value, request.Currency!),
                request.CategoryId,
                request.Description,
                request.Brand);

            if (request.Images is not null)
                foreach (var img in request.Images)
                    product.AddImage(img.Url, img.Alt, img.DisplayOrder);
        }

        await _products.AddAsync(product, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success(product.ToDto(category?.Name, includeInactiveVariants: true));
    }
}
