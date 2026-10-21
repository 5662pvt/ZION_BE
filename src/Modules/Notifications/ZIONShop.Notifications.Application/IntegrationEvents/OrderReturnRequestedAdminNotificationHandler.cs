using Microsoft.Extensions.Logging;
using ZIONShop.Contracts.Auth;
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
/// Admin/Staff fan-out for a new return request, mirroring <see cref="OrderCreatedAdminNotificationHandler"/>.
/// In-app only: no email, no <see cref="ZIONShop.Notifications.Domain.Entities.NotificationLog"/> row.
/// Dedupe is handled by the inbox wrapper this handler is registered through.
/// </summary>
public class OrderReturnRequestedAdminNotificationHandler : IIntegrationEventHandler<OrderReturnRequestedIntegrationEvent>
{
    private readonly IUserNotificationRepository _userNotifications;
    private readonly INotificationsUnitOfWork _uow;
    private readonly IOrderDetailsReader _orderDetails;
    private readonly IStaffUserReader _staffUsers;
    private readonly INotificationPush _push;
    private readonly ILogger<OrderReturnRequestedAdminNotificationHandler> _logger;

    public OrderReturnRequestedAdminNotificationHandler(
        IUserNotificationRepository userNotifications,
        INotificationsUnitOfWork uow,
        IOrderDetailsReader orderDetails,
        IStaffUserReader staffUsers,
        INotificationPush push,
        ILogger<OrderReturnRequestedAdminNotificationHandler> logger)
    {
        _userNotifications = userNotifications;
        _uow = uow;
        _orderDetails = orderDetails;
        _staffUsers = staffUsers;
        _push = push;
        _logger = logger;
    }

    public async Task HandleAsync(OrderReturnRequestedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var order = await _orderDetails.GetAsync(@event.OrderId, cancellationToken);
        if (order is null)
        {
            _logger.LogWarning("OrderReturnRequestedAdminNotificationHandler skipped: order {OrderId} not found", @event.OrderId);
            return;
        }

        var staffUsers = await _staffUsers.GetAdminAndStaffUserIdsAsync(cancellationToken);
        if (staffUsers.Count == 0) return;

        var (title, message) = UserNotificationTemplates.ReturnRequested(order.Code, @event.Reason);
        var notifications = staffUsers
            .Select(staff => UserNotification.Create(staff.UserId, UserNotificationType.ReturnRequested, title, message, @event.OrderId))
            .ToList();

        foreach (var notification in notifications)
            await _userNotifications.AddAsync(notification, cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);

        await Task.WhenAll(notifications.Select(notification => PushAsync(notification, cancellationToken)));
    }

    private async Task PushAsync(UserNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            var dto = new UserNotificationPushDto(notification.Id, notification.Type.ToString(), notification.Title, notification.Message, notification.OrderId, notification.CreatedAt);
            await _push.PublishAsync(notification.UserId, dto, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification push failed for user {UserId}, notification {NotificationId}; continuing", notification.UserId, notification.Id);
        }
    }
}
