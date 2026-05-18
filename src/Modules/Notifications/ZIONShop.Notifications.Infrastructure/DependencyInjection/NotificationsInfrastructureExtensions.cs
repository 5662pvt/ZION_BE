using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ZIONShop.Notifications.Infrastructure.DependencyInjection;

public static class NotificationsInfrastructureExtensions
{
    public static IServiceCollection AddNotificationsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // TODO: register DbContext and repositories when this module is implemented.
        return services;
    }
}
