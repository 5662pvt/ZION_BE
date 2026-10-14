using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZIONShop.Inventory.Application.Services;

namespace ZIONShop.Inventory.Application.DependencyInjection;

public static class InventoryApplicationExtensions
{
    public static IServiceCollection AddInventoryApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ReservationExpiryProcessor>();

        // Integration event handlers are registered in InventoryInfrastructureExtensions, since the
        // inbox-idempotent registration overload needs InventoryDbContext (an Infrastructure type).

        return services;
    }
}
