using MediatR;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.Products.Application.Mappings;
using ZIONShop.Products.Domain.Exceptions;
using ZIONShop.Products.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.AdminGetProduct;

public class AdminGetProductByIdQueryHandler : IRequestHandler<AdminGetProductByIdQuery, Result<ProductDto>>
{
    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;

    public AdminGetProductByIdQueryHandler(IProductRepository products, ICategoryRepository categories)
    {
        _products = products;
        _categories = categories;
    }

    public async Task<Result<ProductDto>> Handle(AdminGetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.Id, cancellationToken);
        if (product is null)
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
