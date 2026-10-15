using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;
using ZIONShop.Caching;

namespace ZIONShop.Products.Application.DependencyInjection;

public static class ProductsApplicationExtensions
{
    public static IServiceCollection AddProductsApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddAutoMapper(assembly);
        services.TryAddSingleton<IDistributedCacheService, NullDistributedCacheService>();
        return services;
    }
}
