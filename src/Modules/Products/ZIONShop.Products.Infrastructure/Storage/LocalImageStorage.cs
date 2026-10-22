using Microsoft.Extensions.Options;
using ZIONShop.Products.Application.Interfaces;
using ZIONShop.Products.Application.Options;
using ZIONShop.Products.Domain.Exceptions;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Infrastructure.Storage;

public class LocalImageStorage : IImageStorage
{
    private const int SignatureHeaderLength = 12;

    private readonly IUploadsRootProvider _rootProvider;
    private readonly ImageStorageOptions _options;

    public LocalImageStorage(IUploadsRootProvider rootProvider, IOptions<ImageStorageOptions> options)
    {
        _rootProvider = rootProvider;
        _options = options.Value;
    }

    public async Task<Result<StoredImage>> UploadAsync(Stream content, string fileName, string contentType, CancellationToken ct, string folder = "products")
    {
        if (!ImageSignature.IsValidFolderName(folder))
            return Result.Failure<StoredImage>(ProductsErrors.InvalidImageUpload);

        // The upload's real format is detected from its magic bytes -- never trusted from the
        // client-supplied Content-Type or filename extension -- and that detected type, not the
        // client's filename, decides the stored extension.
        var (source, buffered) = await EnsureSeekableAsync(content, ct);
        try
        {
            var header = new byte[SignatureHeaderLength];
            var read = await source.ReadAsync(header, 0, header.Length, ct);
            source.Position = 0;

            var detectedType = ImageSignature.Detect(header.AsSpan(0, read));
            if (detectedType is null)
                return Result.Failure<StoredImage>(ProductsErrors.InvalidImageUpload);

            var ext = ImageSignature.ExtensionFor(detectedType);
            var storedFileName = $"{Guid.NewGuid():N}{ext}";
            var relativePath = Path.Combine(folder, storedFileName).Replace('\\', '/');
            var fullPath = Path.Combine(_rootProvider.RootPath, folder, storedFileName);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            await source.CopyToAsync(fileStream, ct);

            var url = $"/{ImageStorageOptions.UploadsFolderName}/{relativePath}";
            if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
                url = $"{_options.PublicBaseUrl.TrimEnd('/')}{url}";

            return Result.Success(new StoredImage(url, relativePath));
        }
        finally
        {
            buffered?.Dispose();
        }
    }

    private static async Task<(Stream Source, MemoryStream? Buffered)> EnsureSeekableAsync(Stream content, CancellationToken ct)
    {
        if (content.CanSeek)
            return (content, null);

        var buffered = new MemoryStream();
        await content.CopyToAsync(buffered, ct);
        buffered.Position = 0;
        return (buffered, buffered);
    }

    public Task DeleteAsync(string publicId, CancellationToken ct)
    {
        var relative = publicId.Replace('\\', '/');
        var legacyPrefix = $"{ImageStorageOptions.UploadsFolderName}/";
        if (relative.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
            relative = relative[legacyPrefix.Length..];

        var fullPath = Path.Combine(_rootProvider.RootPath, relative.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }
}
