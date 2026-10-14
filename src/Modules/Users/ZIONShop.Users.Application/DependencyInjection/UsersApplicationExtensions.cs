using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
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
        // UserRegisteredIntegrationEvent handler is registered in UsersInfrastructureExtensions, since
        // the inbox-idempotent registration overload needs UsersDbContext (an Infrastructure type).
        services.AddScoped<IUserProfileProvisioner, UserProfileProvisioner>();
        return services;
    }
}
