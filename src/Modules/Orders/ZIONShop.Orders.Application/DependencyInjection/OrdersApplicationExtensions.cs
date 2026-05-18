using Microsoft.Extensions.DependencyInjection;

namespace ZIONShop.Orders.Application.DependencyInjection;

public static class OrdersApplicationExtensions
{
    public static IServiceCollection AddOrdersApplication(this IServiceCollection services)
    {
        // TODO: register MediatR/Validators/AutoMapper when this module is implemented.
        return services;
    }
}
