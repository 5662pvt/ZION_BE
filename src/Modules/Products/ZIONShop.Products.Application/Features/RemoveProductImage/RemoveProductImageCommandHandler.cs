using MediatR;
using Microsoft.Extensions.Logging;
using ZIONShop.Caching;
using ZIONShop.Products.Application.Caching;
using ZIONShop.Products.Application.Interfaces;
using ZIONShop.Products.Domain.Exceptions;
using ZIONShop.Products.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.RemoveProductImage;

public class RemoveProductImageCommandHandler : IRequestHandler<RemoveProductImageCommand, Result>
{
    private readonly IProductRepository _products;
    private readonly IImageStorage _imageStorage;
    private readonly IProductsUnitOfWork _uow;
    private readonly IDistributedCacheService _cache;
    private readonly ILogger<RemoveProductImageCommandHandler> _logger;

    public RemoveProductImageCommandHandler(
        IProductRepository products,
        IImageStorage imageStorage,
        IProductsUnitOfWork uow,
        IDistributedCacheService cache,
        ILogger<RemoveProductImageCommandHandler> logger)
    {
        _products = products;
        _imageStorage = imageStorage;
        _uow = uow;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result> Handle(RemoveProductImageCommand request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null) return Result.Failure(ProductsErrors.ProductNotFound);

        var removeResult = product.RemoveImage(request.ImageId);
        if (removeResult.IsFailure) return Result.Failure(removeResult.Error);

        _products.Update(product);
        await _uow.SaveChangesAsync(cancellationToken);

        await _cache.RemoveManyAsync(ProductCacheKeys.DetailById(product.Id), ProductCacheKeys.DetailBySlug(product.Slug));

        var publicId = removeResult.Value.PublicId;
        if (!string.IsNullOrWhiteSpace(publicId))
        {
            try
            {
                await _imageStorage.DeleteAsync(publicId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete image {PublicId} from storage for product {ProductId}", publicId, request.ProductId);
            }
        }

        return Result.Success();
    }
}
