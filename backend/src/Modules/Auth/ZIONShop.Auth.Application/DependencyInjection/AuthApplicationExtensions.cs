using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using ZIONShop.Auth.Application.Interfaces;
using ZIONShop.Auth.Application.Services;

namespace ZIONShop.Auth.Application.DependencyInjection;

public static class AuthApplicationExtensions
{
    public static IServiceCollection AddAuthApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddAutoMapper(assembly);
        services.AddSingleton<IOtpHasher, OtpHasher>();
        services.AddScoped<AuthOtpDeliveryService>();
        services.AddScoped<AuthTokenIssuer>();
        return services;
    }
}
