using MediatR;
using ZIONShop.Notifications.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Notifications.Application.Features.GetUnreadNotificationCount;

public class GetUnreadNotificationCountQueryHandler : IRequestHandler<GetUnreadNotificationCountQuery, Result<int>>
{
    private readonly IUserNotificationRepository _notifications;

    public GetUnreadNotificationCountQueryHandler(IUserNotificationRepository notifications) => _notifications = notifications;

    public async Task<Result<int>> Handle(GetUnreadNotificationCountQuery request, CancellationToken cancellationToken)
    {
        var count = await _notifications.GetUnreadCountAsync(request.UserId, cancellationToken);
        return Result.Success(count);
    }
}
