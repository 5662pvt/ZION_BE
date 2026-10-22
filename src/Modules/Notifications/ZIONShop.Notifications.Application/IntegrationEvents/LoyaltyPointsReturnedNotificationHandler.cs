using Microsoft.Extensions.Logging;
using ZIONShop.Contracts.Loyalty;
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
/// In-app only: no email, no <see cref="ZIONShop.Notifications.Domain.Entities.NotificationLog"/> row.
/// Dedupe is handled by the inbox wrapper this handler is registered through; no manual idempotency
/// check needed here.
/// </summary>
public class LoyaltyPointsReturnedNotificationHandler : IIntegrationEventHandler<LoyaltyPointsReturnedIntegrationEvent>
{
    private readonly IUserNotificationRepository _userNotifications;
    private readonly INotificationsUnitOfWork _uow;
    private readonly IOrderDetailsReader _orderDetails;
    private readonly INotificationPush _push;
    private readonly ILogger<LoyaltyPointsReturnedNotificationHandler> _logger;

    public LoyaltyPointsReturnedNotificationHandler(
        IUserNotificationRepository userNotifications,
        INotificationsUnitOfWork uow,
        IOrderDetailsReader orderDetails,
        INotificationPush push,
        ILogger<LoyaltyPointsReturnedNotificationHandler> logger)
    {
        _userNotifications = userNotifications;
        _uow = uow;
        _orderDetails = orderDetails;
        _push = push;
        _logger = logger;
    }

    public async Task HandleAsync(LoyaltyPointsReturnedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var order = await _orderDetails.GetAsync(@event.OrderId, cancellationToken);
        var orderCode = order?.Code ?? @event.OrderId.ToString()[..8];

        var (title, message) = UserNotificationTemplates.PointsReturned(@event.Points, orderCode, @event.Reason);
        var notification = UserNotification.Create(@event.UserId, UserNotificationType.PointsReturned, title, message, @event.OrderId);
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
