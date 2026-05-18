using Microsoft.Extensions.DependencyInjection;

namespace ZIONShop.Admin.Application.DependencyInjection;

public static class AdminApplicationExtensions
{
    public static IServiceCollection AddAdminApplication(this IServiceCollection services)
    {
        // TODO: register MediatR/Validators/AutoMapper when this module is implemented.
        return services;
    }
}
