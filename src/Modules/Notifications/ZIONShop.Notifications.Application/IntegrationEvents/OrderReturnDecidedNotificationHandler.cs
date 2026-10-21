using Microsoft.Extensions.Logging;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Notifications.Application.Interfaces;
using ZIONShop.Notifications.Application.Templates;
using ZIONShop.Notifications.Domain.Entities;
using ZIONShop.Notifications.Domain.Enums;
using ZIONShop.Notifications.Domain.Repositories;
using ZIONShop.Realtime;

namespace ZIONShop.Notifications.Application.IntegrationEvents;

/// <summary>
/// Notifies the customer once admin approves or rejects their return request. In-app only: no email,
/// no <see cref="ZIONShop.Notifications.Domain.Entities.NotificationLog"/> row. Dedupe is handled by
/// the inbox wrapper this handler is registered through.
/// </summary>
public class OrderReturnDecidedNotificationHandler : IIntegrationEventHandler<OrderReturnDecidedIntegrationEvent>
{
    private readonly IUserNotificationRepository _userNotifications;
    private readonly INotificationsUnitOfWork _uow;
    private readonly IOrderDetailsReader _orderDetails;
    private readonly INotificationPush _push;
    private readonly ILogger<OrderReturnDecidedNotificationHandler> _logger;

    public OrderReturnDecidedNotificationHandler(
        IUserNotificationRepository userNotifications,
        INotificationsUnitOfWork uow,
        IOrderDetailsReader orderDetails,
        INotificationPush push,
        ILogger<OrderReturnDecidedNotificationHandler> logger)
    {
        _userNotifications = userNotifications;
        _uow = uow;
        _orderDetails = orderDetails;
        _push = push;
        _logger = logger;
    }

    public async Task HandleAsync(OrderReturnDecidedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var order = await _orderDetails.GetAsync(@event.OrderId, cancellationToken);
        if (order is null)
        {
            _logger.LogWarning("OrderReturnDecidedNotificationHandler skipped: order {OrderId} not found", @event.OrderId);
            return;
        }

        var (title, message) = @event.Approved
            ? UserNotificationTemplates.ReturnApproved(order.Code)
            : UserNotificationTemplates.ReturnRejected(order.Code, @event.RejectReason ?? "Không có lý do cụ thể");
        var type = @event.Approved ? UserNotificationType.ReturnApproved : UserNotificationType.ReturnRejected;

        var notification = UserNotification.Create(@event.UserId, type, title, message, @event.OrderId);
        await _userNotifications.AddAsync(notification, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        try
        {
            var dto = new UserNotificationPushDto(notification.Id, notification.Type.ToString(), notification.Title, notification.Message, notification.OrderId, notification.CreatedAt);
            await _push.PublishAsync(@event.UserId, dto, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification push failed for user {UserId}, notification {NotificationId}; continuing", @event.UserId, notification.Id);
        }
    }
}
