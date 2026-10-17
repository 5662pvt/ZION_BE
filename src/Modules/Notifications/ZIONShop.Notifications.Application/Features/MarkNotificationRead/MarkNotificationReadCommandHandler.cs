using MediatR;
using ZIONShop.Notifications.Application.Interfaces;
using ZIONShop.Notifications.Domain.Exceptions;
using ZIONShop.Notifications.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Notifications.Application.Features.MarkNotificationRead;

public class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand, Result>
{
    private readonly IUserNotificationRepository _notifications;
    private readonly INotificationsUnitOfWork _uow;

    public MarkNotificationReadCommandHandler(IUserNotificationRepository notifications, INotificationsUnitOfWork uow)
    {
        _notifications = notifications;
        _uow = uow;
    }

    public async Task<Result> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await _notifications.GetByIdForUserAsync(request.Id, request.UserId, cancellationToken);
        if (notification is null) return Result.Failure(NotificationsErrors.UserNotificationNotFound);

        notification.MarkAsRead();
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
