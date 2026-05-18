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
        if (await _products.SkuExistsAsync(request.Sku.Trim().ToUpperInvariant(), cancellationToken))
            return Result.Failure<ProductDto>(ProductsErrors.SkuAlreadyExists);
        if (await _products.SlugExistsAsync(request.Slug.Trim().ToLowerInvariant(), cancellationToken))
            return Result.Failure<ProductDto>(ProductsErrors.SlugAlreadyExists);

        Category? category = null;
        if (request.CategoryId.HasValue)
        {
            category = await _categories.GetByIdAsync(request.CategoryId.Value, cancellationToken);
            if (category is null) return Result.Failure<ProductDto>(ProductsErrors.CategoryNotFound);
        }

        var product = Product.Create(
            request.Name,
            request.Slug,
            Sku.Create(request.Sku),
            Money.Create(request.Price, request.Currency),
            request.CategoryId,
            request.Description,
            request.Brand);

        if (request.Images is not null)
        {
            foreach (var img in request.Images)
                product.AddImage(img.Url, img.Alt, img.DisplayOrder);
        }
        product.Publish();

        await _products.AddAsync(product, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success(product.ToDto(category?.Name));
    }
}
