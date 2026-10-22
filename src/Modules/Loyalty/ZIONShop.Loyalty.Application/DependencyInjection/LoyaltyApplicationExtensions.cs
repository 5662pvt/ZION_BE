using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ZIONShop.Loyalty.Application.DependencyInjection;

public static class LoyaltyApplicationExtensions
{
    public static IServiceCollection AddLoyaltyApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        return services;
    }
}
