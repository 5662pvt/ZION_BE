using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using ZIONShop.Contracts.Auth;
using ZIONShop.EventBus.DependencyInjection;
using ZIONShop.Users.Application.IntegrationEventHandlers;
using ZIONShop.Users.Application.Services;

namespace ZIONShop.Users.Application.DependencyInjection;

public static class UsersApplicationExtensions
{
    public static IServiceCollection AddUsersApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddAutoMapper(assembly);
        services.AddIntegrationEventHandler<UserRegisteredIntegrationEvent, UserRegisteredIntegrationEventHandler>();
        services.AddScoped<IUserProfileProvisioner, UserProfileProvisioner>();
        return services;
    }
}
