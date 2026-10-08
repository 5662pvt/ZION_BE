using MediatR;
using ZIONShop.Products.Application.Interfaces;
using ZIONShop.Products.Domain.Exceptions;
using ZIONShop.Products.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.ReorderProductImages;

public class ReorderProductImagesCommandHandler : IRequestHandler<ReorderProductImagesCommand, Result>
{
    private readonly IProductRepository _products;
    private readonly IProductsUnitOfWork _uow;

    public ReorderProductImagesCommandHandler(IProductRepository products, IProductsUnitOfWork uow)
    {
        _products = products;
        _uow = uow;
    }

    public async Task<Result> Handle(ReorderProductImagesCommand request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null) return Result.Failure(ProductsErrors.ProductNotFound);

        var reorderResult = product.ReorderImages(request.ImageIds);
        if (reorderResult.IsFailure) return Result.Failure(reorderResult.Error);

        _products.Update(product);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
