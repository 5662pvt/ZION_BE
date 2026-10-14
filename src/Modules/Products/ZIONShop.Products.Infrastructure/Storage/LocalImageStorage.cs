using Microsoft.Extensions.Options;
using ZIONShop.Products.Application.Interfaces;
using ZIONShop.Products.Application.Options;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Infrastructure.Storage;

public class LocalImageStorage : IImageStorage
{
    private readonly IUploadsRootProvider _rootProvider;
    private readonly ImageStorageOptions _options;

    public LocalImageStorage(IUploadsRootProvider rootProvider, IOptions<ImageStorageOptions> options)
    {
        _rootProvider = rootProvider;
        _options = options.Value;
    }

    public async Task<Result<StoredImage>> UploadAsync(Stream content, string fileName, string contentType, CancellationToken ct)
    {
        var ext = Path.GetExtension(fileName);
        var storedFileName = $"{Guid.NewGuid():N}{ext}";
        var relativePath = Path.Combine("products", storedFileName).Replace('\\', '/');
        var fullPath = Path.Combine(_rootProvider.RootPath, "products", storedFileName);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        await content.CopyToAsync(fileStream, ct);

        var url = $"/{ImageStorageOptions.UploadsFolderName}/{relativePath}";
        if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
            url = $"{_options.PublicBaseUrl.TrimEnd('/')}{url}";

        return Result.Success(new StoredImage(url, relativePath));
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
