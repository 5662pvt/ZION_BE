using Microsoft.Extensions.Options;
using ZIONShop.Common.Email;
using ZIONShop.Contracts.Auth;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Notifications.Application.Interfaces;
using ZIONShop.Notifications.Application.Options;
using ZIONShop.Notifications.Application.Templates;
using ZIONShop.Notifications.Domain.Entities;
using ZIONShop.Notifications.Domain.Enums;
using ZIONShop.Notifications.Domain.Repositories;

namespace ZIONShop.Notifications.Application.IntegrationEvents;

public class OrderCancelledIntegrationEventHandler : IIntegrationEventHandler<OrderCancelledIntegrationEvent>
{
    private readonly INotificationLogRepository _logs;
    private readonly INotificationsUnitOfWork _uow;
    private readonly IUserContactReader _contacts;
    private readonly IOrderDetailsReader _orderDetails;
    private readonly IEmailSender _email;
    private readonly NotificationsOptions _options;

    public OrderCancelledIntegrationEventHandler(
        INotificationLogRepository logs,
        INotificationsUnitOfWork uow,
        IUserContactReader contacts,
        IOrderDetailsReader orderDetails,
        IEmailSender email,
        IOptions<NotificationsOptions> options)
    {
        _logs = logs;
        _uow = uow;
        _contacts = contacts;
        _orderDetails = orderDetails;
        _email = email;
        _options = options.Value;
    }

    public async Task HandleAsync(OrderCancelledIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        if (await _logs.ExistsAsync(@event.EventId, NotificationType.OrderCancelled, cancellationToken)) return;

        var contact = await _contacts.GetAsync(@event.UserId, cancellationToken);
        var order = await _orderDetails.GetAsync(@event.OrderId, cancellationToken);

        if (contact is null || string.IsNullOrWhiteSpace(contact.Email) || order is null)
        {
            var reason = order is null ? "Order not found" : "Missing contact email";
            await _logs.AddAsync(NotificationLog.Skipped(@event.EventId, NotificationType.OrderCancelled, @event.UserId, @event.OrderId, reason, DateTime.UtcNow), cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return;
        }

        var subject = OrderEmailTemplates.CancelledSubject(order.Code);
        var body = OrderEmailTemplates.BuildCancelledBody(contact.DisplayName, order with { CancelReason = order.CancelReason ?? @event.Reason }, _options.StorefrontBaseUrl);

        NotificationLog log;
        try
        {
            await _email.SendAsync(contact.Email, subject, body, cancellationToken);
            log = NotificationLog.Sent(@event.EventId, NotificationType.OrderCancelled, @event.UserId, @event.OrderId, contact.Email, subject, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            log = NotificationLog.Failed(@event.EventId, NotificationType.OrderCancelled, @event.UserId, @event.OrderId, contact.Email, subject, ex.Message, DateTime.UtcNow);
        }

        await _logs.AddAsync(log, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
    }
}
