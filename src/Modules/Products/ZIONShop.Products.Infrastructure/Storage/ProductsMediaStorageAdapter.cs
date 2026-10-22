using ZIONShop.Contracts.Media;
using ZIONShop.Products.Application.Interfaces;

namespace ZIONShop.Products.Infrastructure.Storage;

/// <summary>
/// Adapts the Products module's own <see cref="IImageStorage"/> (Local disk or Cloudinary) to the
/// cross-module <see cref="IMediaStorage"/> contract so other modules (e.g. Chat) can upload images
/// without depending on Products.Infrastructure directly. <paramref name="folder"/> is forwarded as-is
/// to <see cref="IImageStorage.UploadAsync"/> (e.g. "products" for Products, "chat" for Chat).
/// </summary>
public class ProductsMediaStorageAdapter : IMediaStorage
{
    private readonly IImageStorage _imageStorage;

    public ProductsMediaStorageAdapter(IImageStorage imageStorage) => _imageStorage = imageStorage;

    public async Task<(StoredMedia? Media, string? Error)> UploadImageAsync(Stream content, string fileName, string contentType, string folder, CancellationToken ct = default)
    {
        var result = await _imageStorage.UploadAsync(content, fileName, contentType, ct, folder);
        return result.IsFailure
            ? (null, result.Error.Message)
            : (new StoredMedia(result.Value.Url, result.Value.PublicId), null);
    }
}
