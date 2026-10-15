using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZIONShop.Caching;

namespace ZIONShop.Admin.Application.DependencyInjection;

public static class AdminApplicationExtensions
{
    public static IServiceCollection AddAdminApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IDistributedCacheService, NullDistributedCacheService>();
        return services;
    }
}
