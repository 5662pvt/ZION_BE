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
    private const string FolderPrefix = "zionshop";
    private const int SignatureHeaderLength = 12;

    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryImageStorage> _logger;

    public CloudinaryImageStorage(IOptions<CloudinaryOptions> options, ILogger<CloudinaryImageStorage> logger)
    {
        var o = options.Value;
        _cloudinary = new Cloudinary(new Account(o.CloudName, o.ApiKey, o.ApiSecret));
        _logger = logger;
    }

    public async Task<Result<StoredImage>> UploadAsync(Stream content, string fileName, string contentType, CancellationToken ct, string folder = "products")
    {
        if (!ImageSignature.IsValidFolderName(folder))
            return Result.Failure<StoredImage>(ProductsErrors.InvalidImageUpload);

        MemoryStream? buffered = null;
        try
        {
            // The upload's real format is detected from its magic bytes -- never trusted from the
            // client-supplied Content-Type or filename extension.
            var source = content;
            if (!content.CanSeek)
            {
                buffered = new MemoryStream();
                await content.CopyToAsync(buffered, ct);
                buffered.Position = 0;
                source = buffered;
            }

            var header = new byte[SignatureHeaderLength];
            var read = await source.ReadAsync(header, 0, header.Length, ct);
            source.Position = 0;

            var detectedType = ImageSignature.Detect(header.AsSpan(0, read));
            if (detectedType is null)
                return Result.Failure<StoredImage>(ProductsErrors.InvalidImageUpload);

            var ext = ImageSignature.ExtensionFor(detectedType);
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription($"{Guid.NewGuid():N}{ext}", source),
                Folder = $"{FolderPrefix}/{folder}"
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
        finally
        {
            buffered?.Dispose();
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
