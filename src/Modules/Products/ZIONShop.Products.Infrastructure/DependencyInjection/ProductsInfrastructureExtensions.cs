using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZIONShop.Contracts.Media;
using ZIONShop.Contracts.Products;
using ZIONShop.Products.Application.Interfaces;
using ZIONShop.Products.Application.Options;
using ZIONShop.Products.Domain.Repositories;
using ZIONShop.Products.Infrastructure.Persistence;
using ZIONShop.Products.Infrastructure.Readers;
using ZIONShop.Products.Infrastructure.Repositories;
using ZIONShop.Products.Infrastructure.Services;
using ZIONShop.Products.Infrastructure.Storage;

namespace ZIONShop.Products.Infrastructure.DependencyInjection;

public static class ProductsInfrastructureExtensions
{
    public static IServiceCollection AddProductsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ProductsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ProductsDbContext.Schema)));

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProductsUnitOfWork>(sp => sp.GetRequiredService<ProductsDbContext>());
        services.AddScoped<IProductPriceLookup, ProductPriceLookup>();
        services.AddScoped<IProductCatalogReader, ProductCatalogReader>();
        services.AddScoped<ICategoryLookup, CategoryLookup>();

        var cloudinarySection = configuration.GetSection(CloudinaryOptions.SectionName);
        services.Configure<CloudinaryOptions>(cloudinarySection);
        services.Configure<ImageStorageOptions>(configuration.GetSection(ImageStorageOptions.SectionName));
        services.AddSingleton<IUploadsRootProvider, UploadsRootProvider>();

        var cloudName = cloudinarySection[nameof(CloudinaryOptions.CloudName)];
        var apiKey = cloudinarySection[nameof(CloudinaryOptions.ApiKey)];
        var apiSecret = cloudinarySection[nameof(CloudinaryOptions.ApiSecret)];
        var cloudinaryConfigured = !string.IsNullOrWhiteSpace(cloudName) && cloudName != "CHANGE_ME"
            && !string.IsNullOrWhiteSpace(apiKey) && apiKey != "CHANGE_ME"
            && !string.IsNullOrWhiteSpace(apiSecret) && apiSecret != "CHANGE_ME";

        if (cloudinaryConfigured)
        {
            services.AddScoped<IImageStorage>(sp =>
            {
                sp.GetRequiredService<ILogger<CloudinaryImageStorage>>().LogInformation("Products image storage: Cloudinary");
                return ActivatorUtilities.CreateInstance<CloudinaryImageStorage>(sp);
            });
        }
        else
        {
            services.AddScoped<IImageStorage>(sp =>
            {
                sp.GetRequiredService<ILogger<LocalImageStorage>>().LogInformation("Products image storage: Local disk");
                return ActivatorUtilities.CreateInstance<LocalImageStorage>(sp);
            });
        }

        services.AddScoped<IMediaStorage, ProductsMediaStorageAdapter>();

        return services;
    }
}
