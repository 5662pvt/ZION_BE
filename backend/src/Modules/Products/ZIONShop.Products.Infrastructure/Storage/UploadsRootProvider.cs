using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ZIONShop.Products.Application.Interfaces;
using ZIONShop.Products.Application.Options;

namespace ZIONShop.Products.Infrastructure.Storage;

public class UploadsRootProvider : IUploadsRootProvider
{
    public string RootPath { get; }

    public UploadsRootProvider(IHostEnvironment env, IOptions<ImageStorageOptions> options)
    {
        var configured = options.Value.LocalRootPath;

        RootPath = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(env.ContentRootPath, "App_Data", "uploads")
            : Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(env.ContentRootPath, configured);
    }
}
