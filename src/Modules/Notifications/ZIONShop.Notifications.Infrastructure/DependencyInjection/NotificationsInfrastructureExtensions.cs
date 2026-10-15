using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.DependencyInjection;
using ZIONShop.EventBus.Inbox;
using ZIONShop.Notifications.Application.IntegrationEvents;
using ZIONShop.Notifications.Application.Interfaces;
using ZIONShop.Notifications.Application.Options;
using ZIONShop.Notifications.Domain.Repositories;
using ZIONShop.Notifications.Infrastructure.Persistence;
using ZIONShop.Notifications.Infrastructure.Repositories;

namespace ZIONShop.Notifications.Infrastructure.DependencyInjection;

public static class NotificationsInfrastructureExtensions
{
    public static IServiceCollection AddNotificationsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<NotificationsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", NotificationsDbContext.Schema)));

        services.Configure<NotificationsOptions>(configuration.GetSection(NotificationsOptions.SectionName));

        services.AddScoped<INotificationLogRepository, NotificationLogRepository>();
        services.AddScoped<INotificationsUnitOfWork>(sp => sp.GetRequiredService<NotificationsDbContext>());

        services.AddInboxAdmin<NotificationsDbContext>();

        services.AddIntegrationEventHandler<OrderConfirmedIntegrationEvent, OrderConfirmedIntegrationEventHandler, NotificationsDbContext>();
        services.AddIntegrationEventHandler<OrderShippedIntegrationEvent, OrderShippedIntegrationEventHandler, NotificationsDbContext>();
        services.AddIntegrationEventHandler<OrderCancelledIntegrationEvent, OrderCancelledIntegrationEventHandler, NotificationsDbContext>();

        return services;
    }
}
