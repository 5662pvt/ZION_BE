using Microsoft.Extensions.DependencyInjection;

namespace ZIONShop.Inventory.Application.DependencyInjection;

public static class InventoryApplicationExtensions
{
    public static IServiceCollection AddInventoryApplication(this IServiceCollection services)
    {
        // TODO: register MediatR/Validators/AutoMapper when this module is implemented.
        return services;
    }
}
