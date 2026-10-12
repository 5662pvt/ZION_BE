using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using ZIONShop.Contracts.Orders;
using ZIONShop.Orders.Application.Services;

namespace ZIONShop.Orders.Application.DependencyInjection;

public static class OrdersApplicationExtensions
{
    public static IServiceCollection AddOrdersApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddScoped<IShippingFeeCalculator, ShippingFeeCalculator>();
        return services;
    }
}
