namespace ZIONShop.Contracts.Media;

public sealed record StoredMedia(string Url, string PublicId);

/// <summary>
/// Cross-module media upload contract. <c>ZIONShop.Contracts</c> does not reference
/// <c>ZIONShop.SharedKernel</c>, so failures are reported via the tuple's <c>Error</c> rather than
/// <c>Result&lt;T&gt;</c>. Type/size validation is the caller's responsibility -- implementations only
/// perform the upload.
/// </summary>
public interface IMediaStorage
{
    Task<(StoredMedia? Media, string? Error)> UploadImageAsync(Stream content, string fileName, string contentType, string folder, CancellationToken ct = default);
}
