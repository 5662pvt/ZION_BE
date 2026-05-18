using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ZIONShop.Inventory.Infrastructure.DependencyInjection;

public static class InventoryInfrastructureExtensions
{
    public static IServiceCollection AddInventoryInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // TODO: register DbContext and repositories when this module is implemented.
        return services;
    }
}
