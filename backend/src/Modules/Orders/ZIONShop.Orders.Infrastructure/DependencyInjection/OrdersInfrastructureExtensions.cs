using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ZIONShop.Orders.Infrastructure.DependencyInjection;

public static class OrdersInfrastructureExtensions
{
    public static IServiceCollection AddOrdersInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // TODO: register DbContext and repositories when this module is implemented.
        return services;
    }
}
