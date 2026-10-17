using Microsoft.Extensions.Logging;
using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Payments;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Notifications.Application.Interfaces;
using ZIONShop.Notifications.Application.Templates;
using ZIONShop.Notifications.Domain.Entities;
using ZIONShop.Notifications.Domain.Enums;
using ZIONShop.Notifications.Domain.Repositories;
using ZIONShop.Realtime;

namespace ZIONShop.Notifications.Application.IntegrationEvents;

/// <summary>
/// <see cref="PaymentCompletedIntegrationEvent"/> carries OrderId but not UserId -- resolved via
/// <see cref="IOrderDetailsReader"/> (same reader the order-status handlers use). In-app only, no
/// email/NotificationLog row. Dedupe handled by the inbox wrapper this handler is registered through.
/// </summary>
public class PaymentCompletedNotificationHandler : IIntegrationEventHandler<PaymentCompletedIntegrationEvent>
{
    private readonly IUserNotificationRepository _userNotifications;
    private readonly INotificationsUnitOfWork _uow;
    private readonly IOrderDetailsReader _orderDetails;
    private readonly INotificationPush _push;
    private readonly ILogger<PaymentCompletedNotificationHandler> _logger;

    public PaymentCompletedNotificationHandler(
        IUserNotificationRepository userNotifications,
        INotificationsUnitOfWork uow,
        IOrderDetailsReader orderDetails,
        INotificationPush push,
        ILogger<PaymentCompletedNotificationHandler> logger)
    {
        _userNotifications = userNotifications;
        _uow = uow;
        _orderDetails = orderDetails;
        _push = push;
        _logger = logger;
    }

    public async Task HandleAsync(PaymentCompletedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var order = await _orderDetails.GetAsync(@event.OrderId, cancellationToken);
        if (order is null)
        {
            _logger.LogWarning("PaymentCompletedNotificationHandler skipped: order {OrderId} not found", @event.OrderId);
            return;
        }

        var (title, message) = UserNotificationTemplates.PaymentCompleted(order.Code);
        var notification = UserNotification.Create(order.UserId, UserNotificationType.PaymentCompleted, title, message, @event.OrderId);
        await _userNotifications.AddAsync(notification, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        try
        {
            var dto = new UserNotificationPushDto(notification.Id, notification.Type.ToString(), notification.Title, notification.Message, notification.OrderId, notification.CreatedAt);
            await _push.PublishAsync(order.UserId, dto, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification push failed for user {UserId}, notification {NotificationId}; continuing", order.UserId, notification.Id);
        }
    }
}
