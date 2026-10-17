using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZIONShop.Contracts.Auth;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Notifications.Application.Interfaces;
using ZIONShop.Notifications.Application.Options;
using ZIONShop.Notifications.Application.Templates;
using ZIONShop.Notifications.Domain.Entities;
using ZIONShop.Notifications.Domain.Enums;
using ZIONShop.Notifications.Domain.Repositories;
using ZIONShop.Common.Email;
using ZIONShop.Realtime;

namespace ZIONShop.Notifications.Application.IntegrationEvents;

public class OrderConfirmedIntegrationEventHandler : IIntegrationEventHandler<OrderConfirmedIntegrationEvent>
{
    private readonly INotificationLogRepository _logs;
    private readonly IUserNotificationRepository _userNotifications;
    private readonly INotificationsUnitOfWork _uow;
    private readonly IUserContactReader _contacts;
    private readonly IOrderDetailsReader _orderDetails;
    private readonly IEmailSender _email;
    private readonly INotificationPush _push;
    private readonly NotificationsOptions _options;
    private readonly ILogger<OrderConfirmedIntegrationEventHandler> _logger;

    public OrderConfirmedIntegrationEventHandler(
        INotificationLogRepository logs,
        IUserNotificationRepository userNotifications,
        INotificationsUnitOfWork uow,
        IUserContactReader contacts,
        IOrderDetailsReader orderDetails,
        IEmailSender email,
        INotificationPush push,
        IOptions<NotificationsOptions> options,
        ILogger<OrderConfirmedIntegrationEventHandler> logger)
    {
        _logs = logs;
        _userNotifications = userNotifications;
        _uow = uow;
        _contacts = contacts;
        _orderDetails = orderDetails;
        _email = email;
        _push = push;
        _options = options.Value;
        _logger = logger;
    }

    public async Task HandleAsync(OrderConfirmedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        if (await _logs.ExistsAsync(@event.EventId, NotificationType.OrderConfirmed, cancellationToken)) return;

        var contact = await _contacts.GetAsync(@event.UserId, cancellationToken);
        var order = await _orderDetails.GetAsync(@event.OrderId, cancellationToken);

        if (contact is null || string.IsNullOrWhiteSpace(contact.Email) || order is null)
        {
            var reason = order is null ? "Order not found" : "Missing contact email";
            await _logs.AddAsync(NotificationLog.Skipped(@event.EventId, NotificationType.OrderConfirmed, @event.UserId, @event.OrderId, reason, DateTime.UtcNow), cancellationToken);

            var userNotification = order is not null
                ? await AddUserNotificationAsync(@event.UserId, order.Code, @event.OrderId, cancellationToken)
                : null;

            await _uow.SaveChangesAsync(cancellationToken);

            if (userNotification is not null) await PushAsync(@event.UserId, userNotification, cancellationToken);
            return;
        }

        var subject = OrderEmailTemplates.ConfirmedSubject(order.Code);
        var body = OrderEmailTemplates.BuildConfirmedBody(contact.DisplayName, order, _options.StorefrontBaseUrl);

        NotificationLog log;
        try
        {
            await _email.SendAsync(contact.Email, subject, body, cancellationToken);
            log = NotificationLog.Sent(@event.EventId, NotificationType.OrderConfirmed, @event.UserId, @event.OrderId, contact.Email, subject, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            log = NotificationLog.Failed(@event.EventId, NotificationType.OrderConfirmed, @event.UserId, @event.OrderId, contact.Email, subject, ex.Message, DateTime.UtcNow);
        }

        await _logs.AddAsync(log, cancellationToken);
        var created = await AddUserNotificationAsync(@event.UserId, order.Code, @event.OrderId, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        await PushAsync(@event.UserId, created, cancellationToken);
    }

    private async Task<UserNotification> AddUserNotificationAsync(Guid userId, string orderCode, Guid orderId, CancellationToken cancellationToken)
    {
        var (title, message) = UserNotificationTemplates.OrderConfirmed(orderCode);
        var notification = UserNotification.Create(userId, UserNotificationType.OrderConfirmed, title, message, orderId);
        await _userNotifications.AddAsync(notification, cancellationToken);
        return notification;
    }

    private async Task PushAsync(Guid userId, UserNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            var dto = new UserNotificationPushDto(notification.Id, notification.Type.ToString(), notification.Title, notification.Message, notification.OrderId, notification.CreatedAt);
            await _push.PublishAsync(userId, dto, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification push failed for user {UserId}, notification {NotificationId}; continuing", userId, notification.Id);
        }
    }
}
