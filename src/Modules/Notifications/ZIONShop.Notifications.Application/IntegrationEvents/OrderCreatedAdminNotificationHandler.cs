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
/// Independent second consumer of <see cref="OrderCreatedIntegrationEvent"/> -- notifies Admin/Staff
/// users that a new order needs review, alongside <see cref="OrderCreatedNotificationHandler"/> which
/// notifies the customer. In-app only: no email, no <see cref="ZIONShop.Notifications.Domain.Entities.NotificationLog"/>
/// row. Dedupe is handled by the inbox wrapper this handler is registered through.
/// </summary>
public class OrderCreatedAdminNotificationHandler : IIntegrationEventHandler<OrderCreatedIntegrationEvent>
{
    private readonly IUserNotificationRepository _userNotifications;
    private readonly INotificationsUnitOfWork _uow;
    private readonly IOrderDetailsReader _orderDetails;
    private readonly IStaffUserReader _staffUsers;
    private readonly INotificationPush _push;
    private readonly ILogger<OrderCreatedAdminNotificationHandler> _logger;

    public OrderCreatedAdminNotificationHandler(
        IUserNotificationRepository userNotifications,
        INotificationsUnitOfWork uow,
        IOrderDetailsReader orderDetails,
        IStaffUserReader staffUsers,
        INotificationPush push,
        ILogger<OrderCreatedAdminNotificationHandler> logger)
    {
        _userNotifications = userNotifications;
        _uow = uow;
        _orderDetails = orderDetails;
        _staffUsers = staffUsers;
        _push = push;
        _logger = logger;
    }

    public async Task HandleAsync(OrderCreatedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var order = await _orderDetails.GetAsync(@event.OrderId, cancellationToken);
        if (order is null)
        {
            _logger.LogWarning("OrderCreatedAdminNotificationHandler skipped: order {OrderId} not found", @event.OrderId);
            return;
        }

        var staffUsers = await _staffUsers.GetAdminAndStaffUserIdsAsync(cancellationToken);
        if (staffUsers.Count == 0) return;

        var (title, message) = UserNotificationTemplates.OrderCreatedForReview(order.Code);
        var notifications = staffUsers
            .Select(staff => UserNotification.Create(staff.UserId, UserNotificationType.OrderCreatedForReview, title, message, @event.OrderId))
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
