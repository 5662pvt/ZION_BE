using MediatR;
using ZIONShop.Notifications.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Notifications.Application.Features.MarkAllNotificationsRead;

public class MarkAllNotificationsReadCommandHandler : IRequestHandler<MarkAllNotificationsReadCommand, Result<int>>
{
    private readonly IUserNotificationRepository _notifications;

    public MarkAllNotificationsReadCommandHandler(IUserNotificationRepository notifications) => _notifications = notifications;

    public async Task<Result<int>> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        var updated = await _notifications.MarkAllAsReadAsync(request.UserId, cancellationToken);
        return Result.Success(updated);
    }
}
