using MediatR;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.Products.Application.Interfaces;
using ZIONShop.Products.Application.Mappings;
using ZIONShop.Products.Domain.Exceptions;
using ZIONShop.Products.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.ArchiveProduct;

public class ArchiveProductCommandHandler : IRequestHandler<ArchiveProductCommand, Result<ProductDto>>
{
    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;
    private readonly IProductsUnitOfWork _uow;

    public ArchiveProductCommandHandler(IProductRepository products, ICategoryRepository categories, IProductsUnitOfWork uow)
    {
        _products = products;
        _categories = categories;
        _uow = uow;
    }

    public async Task<Result<ProductDto>> Handle(ArchiveProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.Id, cancellationToken);
        if (product is null) return Result.Failure<ProductDto>(ProductsErrors.ProductNotFound);

        product.Archive();
        _products.Update(product);
        await _uow.SaveChangesAsync(cancellationToken);

        string? categoryName = null;
        if (product.CategoryId.HasValue)
        {
            var category = await _categories.GetByIdAsync(product.CategoryId.Value, cancellationToken);
            categoryName = category?.Name;
        }

        return Result.Success(product.ToDto(categoryName));
    }
}
