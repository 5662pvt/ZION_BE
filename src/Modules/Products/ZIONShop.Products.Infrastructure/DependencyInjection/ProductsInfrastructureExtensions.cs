using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Products.Application.Interfaces;
using ZIONShop.Products.Domain.Repositories;
using ZIONShop.Products.Infrastructure.Persistence;
using ZIONShop.Products.Infrastructure.Repositories;
using ZIONShop.Products.Infrastructure.Services;

namespace ZIONShop.Products.Infrastructure.DependencyInjection;

public static class ProductsInfrastructureExtensions
{
    public static IServiceCollection AddProductsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ProductsDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"),
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", ProductsDbContext.Schema)));

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProductsUnitOfWork>(sp => sp.GetRequiredService<ProductsDbContext>());
        services.AddScoped<IProductPriceLookup, ProductPriceLookup>();
        return services;
    }
}
