using MediatR;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.Products.Application.Interfaces;
using ZIONShop.Products.Domain.Exceptions;
using ZIONShop.Products.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.AddProductImage;

public class AddProductImageCommandHandler : IRequestHandler<AddProductImageCommand, Result<ProductImageDto>>
{
    private readonly IProductRepository _products;
    private readonly IImageStorage _imageStorage;
    private readonly IProductsUnitOfWork _uow;

    public AddProductImageCommandHandler(IProductRepository products, IImageStorage imageStorage, IProductsUnitOfWork uow)
    {
        _products = products;
        _imageStorage = imageStorage;
        _uow = uow;
    }

    public async Task<Result<ProductImageDto>> Handle(AddProductImageCommand request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null) return Result.Failure<ProductImageDto>(ProductsErrors.ProductNotFound);

        var uploadResult = await _imageStorage.UploadAsync(request.Content, request.FileName, request.ContentType, cancellationToken);
        if (uploadResult.IsFailure) return Result.Failure<ProductImageDto>(uploadResult.Error);

        var image = product.AddImage(uploadResult.Value.Url, request.Alt, uploadResult.Value.PublicId);
        _products.AddImage(image);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success(new ProductImageDto(image.Id, image.Url, image.Alt, image.DisplayOrder));
    }
}
