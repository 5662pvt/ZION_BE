using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace ZIONShop.Products.Application.DependencyInjection;

public static class ProductsApplicationExtensions
{
    public static IServiceCollection AddProductsApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddAutoMapper(assembly);
        return services;
    }
}
