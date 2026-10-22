using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Interfaces;

public record StoredImage(string Url, string PublicId);

public interface IImageStorage
{
    Task<Result<StoredImage>> UploadAsync(Stream content, string fileName, string contentType, CancellationToken ct, string folder = "products");
    Task DeleteAsync(string publicId, CancellationToken ct);
}
