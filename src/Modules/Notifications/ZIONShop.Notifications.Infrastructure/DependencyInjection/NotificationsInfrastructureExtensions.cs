using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Contracts.Loyalty;
using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Payments;
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
        services.AddScoped<IUserNotificationRepository, UserNotificationRepository>();
        services.AddScoped<INotificationsUnitOfWork>(sp => sp.GetRequiredService<NotificationsDbContext>());

        services.AddInboxAdmin<NotificationsDbContext>();

        services.AddIntegrationEventHandler<OrderConfirmedIntegrationEvent, OrderConfirmedIntegrationEventHandler, NotificationsDbContext>();
        services.AddIntegrationEventHandler<OrderShippedIntegrationEvent, OrderShippedIntegrationEventHandler, NotificationsDbContext>();
        services.AddIntegrationEventHandler<OrderCancelledIntegrationEvent, OrderCancelledIntegrationEventHandler, NotificationsDbContext>();
        services.AddIntegrationEventHandler<OrderCreatedIntegrationEvent, OrderCreatedNotificationHandler, NotificationsDbContext>();
        services.AddIntegrationEventHandler<OrderCreatedIntegrationEvent, OrderCreatedAdminNotificationHandler, NotificationsDbContext>();
        services.AddIntegrationEventHandler<OrderCompletedIntegrationEvent, OrderCompletedNotificationHandler, NotificationsDbContext>();
        services.AddIntegrationEventHandler<PaymentCompletedIntegrationEvent, PaymentCompletedNotificationHandler, NotificationsDbContext>();
        services.AddIntegrationEventHandler<PaymentFailedIntegrationEvent, PaymentFailedNotificationHandler, NotificationsDbContext>();
        services.AddIntegrationEventHandler<OrderReturnRequestedIntegrationEvent, OrderReturnRequestedAdminNotificationHandler, NotificationsDbContext>();
        services.AddIntegrationEventHandler<OrderReturnDecidedIntegrationEvent, OrderReturnDecidedNotificationHandler, NotificationsDbContext>();
        services.AddIntegrationEventHandler<OrderReturnShippedBackIntegrationEvent, OrderReturnShippedBackAdminNotificationHandler, NotificationsDbContext>();
        services.AddIntegrationEventHandler<OrderRefundedIntegrationEvent, OrderRefundedNotificationHandler, NotificationsDbContext>();
        services.AddIntegrationEventHandler<LoyaltyPointsEarnedIntegrationEvent, LoyaltyPointsEarnedNotificationHandler, NotificationsDbContext>();
        services.AddIntegrationEventHandler<LoyaltyPointsReturnedIntegrationEvent, LoyaltyPointsReturnedNotificationHandler, NotificationsDbContext>();

        return services;
    }
}
