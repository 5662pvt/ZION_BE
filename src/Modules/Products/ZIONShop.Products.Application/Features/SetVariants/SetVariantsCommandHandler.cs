using MediatR;
using ZIONShop.Caching;
using ZIONShop.Products.Application.Caching;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.Products.Application.Interfaces;
using ZIONShop.Products.Application.Mappings;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.Exceptions;
using ZIONShop.Products.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.SetVariants;

public class SetVariantsCommandHandler : IRequestHandler<SetVariantsCommand, Result<ProductDto>>
{
    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;
    private readonly IProductsUnitOfWork _uow;
    private readonly IDistributedCacheService _cache;

    public SetVariantsCommandHandler(IProductRepository products, ICategoryRepository categories, IProductsUnitOfWork uow, IDistributedCacheService cache)
    {
        _products = products;
        _categories = categories;
        _uow = uow;
        _cache = cache;
    }

    public async Task<Result<ProductDto>> Handle(SetVariantsCommand request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null) return Result.Failure<ProductDto>(ProductsErrors.ProductNotFound);

        foreach (var variant in request.Variants)
        {
            if (await _products.SkuExistsAsync(variant.Sku.Trim().ToUpperInvariant(), request.ProductId, cancellationToken))
                return Result.Failure<ProductDto>(ProductsErrors.SkuAlreadyExists);
        }

        var optionInputs = request.Options.Select(o => new ProductOptionInput(o.Name, o.Values)).ToList();
        var variantInputs = request.Variants
            .Select(v => new ProductVariantInput(v.Id, v.Option1Value, v.Option2Value, v.Sku, v.Price, v.Currency, v.IsActive, v.ImageId))
            .ToList();

        var variantIdsBefore = product.Variants.Select(v => v.Id).ToHashSet();

        var setResult = product.SetVariants(optionInputs, variantInputs);
        if (setResult.IsFailure) return Result.Failure<ProductDto>(setResult.Error);

        // product is already tracked (loaded via GetByIdAsync on this context): EF's automatic change
        // detection reliably updates existing tracked variants, but cannot be trusted to classify
        // brand-new ones (client-generated Guid keys) as Added rather than Modified — they must be
        // registered explicitly, same as AddImage.
        foreach (var variant in product.Variants.Where(v => !variantIdsBefore.Contains(v.Id)))
            _products.AddVariant(variant);

        await _uow.SaveChangesAsync(cancellationToken);

        await _cache.RemoveManyAsync(ProductCacheKeys.DetailById(product.Id), ProductCacheKeys.DetailBySlug(product.Slug));

        string? categoryName = null;
        if (product.CategoryId.HasValue)
        {
            var category = await _categories.GetByIdAsync(product.CategoryId.Value, cancellationToken);
            categoryName = category?.Name;
        }

        return Result.Success(product.ToDto(categoryName, includeInactiveVariants: true));
    }
}
