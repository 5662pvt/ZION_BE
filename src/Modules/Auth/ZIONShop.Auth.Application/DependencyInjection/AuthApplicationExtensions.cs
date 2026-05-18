using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace ZIONShop.Auth.Application.DependencyInjection;

public static class AuthApplicationExtensions
{
    public static IServiceCollection AddAuthApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddAutoMapper(assembly);
        return services;
    }
}
