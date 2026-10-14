using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZIONShop.Products.Application.Interfaces;
using ZIONShop.Products.Application.Options;
using ZIONShop.Products.Domain.Exceptions;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Infrastructure.Storage;

public class CloudinaryImageStorage : IImageStorage
{
    private const string Folder = "zionshop/products";

    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryImageStorage> _logger;

    public CloudinaryImageStorage(IOptions<CloudinaryOptions> options, ILogger<CloudinaryImageStorage> logger)
    {
        var o = options.Value;
        _cloudinary = new Cloudinary(new Account(o.CloudName, o.ApiKey, o.ApiSecret));
        _logger = logger;
    }

    public async Task<Result<StoredImage>> UploadAsync(Stream content, string fileName, string contentType, CancellationToken ct)
    {
        try
        {
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, content),
                Folder = Folder
            };

            var result = await _cloudinary.UploadAsync(uploadParams);
            if (result.Error is not null || result.StatusCode != System.Net.HttpStatusCode.OK)
            {
                _logger.LogWarning("Cloudinary upload failed: {Error}", result.Error?.Message);
                return Result.Failure<StoredImage>(ProductsErrors.InvalidImageUpload);
            }

            return Result.Success(new StoredImage(result.SecureUrl.ToString(), result.PublicId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cloudinary upload threw an exception");
            return Result.Failure<StoredImage>(ProductsErrors.InvalidImageUpload);
        }
    }

    public async Task DeleteAsync(string publicId, CancellationToken ct)
    {
        try
        {
            await _cloudinary.DestroyAsync(new DeletionParams(publicId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete Cloudinary image {PublicId}", publicId);
        }
    }
}
