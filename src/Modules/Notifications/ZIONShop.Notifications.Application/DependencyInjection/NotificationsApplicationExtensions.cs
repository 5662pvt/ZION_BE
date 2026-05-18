using Microsoft.Extensions.DependencyInjection;

namespace ZIONShop.Notifications.Application.DependencyInjection;

public static class NotificationsApplicationExtensions
{
    public static IServiceCollection AddNotificationsApplication(this IServiceCollection services)
    {
        // TODO: register MediatR/Validators/AutoMapper when this module is implemented.
        return services;
    }
}
