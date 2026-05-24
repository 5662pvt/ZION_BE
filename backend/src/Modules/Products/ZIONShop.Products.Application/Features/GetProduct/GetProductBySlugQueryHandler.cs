using MediatR;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.Products.Application.Mappings;
using ZIONShop.Products.Domain.Enums;
using ZIONShop.Products.Domain.Exceptions;
using ZIONShop.Products.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.GetProduct;

public class GetProductBySlugQueryHandler : IRequestHandler<GetProductBySlugQuery, Result<ProductDto>>
{
    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;

    public GetProductBySlugQueryHandler(IProductRepository products, ICategoryRepository categories)
    {
        _products = products;
        _categories = categories;
    }

    public async Task<Result<ProductDto>> Handle(GetProductBySlugQuery request, CancellationToken cancellationToken)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();
        var product = await _products.GetBySlugAsync(slug, cancellationToken);
        if (product is null || product.Status != ProductStatus.Published)
            return Result.Failure<ProductDto>(ProductsErrors.ProductNotFound);

        string? categoryName = null;
        if (product.CategoryId.HasValue)
        {
            var category = await _categories.GetByIdAsync(product.CategoryId.Value, cancellationToken);
            categoryName = category?.Name;
        }
        return Result.Success(product.ToDto(categoryName));
    }
}
