using MediatR;
using ZIONShop.Common.Pagination;
using ZIONShop.Notifications.Application.DTOs;
using ZIONShop.Notifications.Application.Mappings;
using ZIONShop.Notifications.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Notifications.Application.Features.GetNotificationFeed;

public class GetNotificationFeedQueryHandler : IRequestHandler<GetNotificationFeedQuery, Result<PagedResult<UserNotificationDto>>>
{
    private readonly IUserNotificationRepository _notifications;

    public GetNotificationFeedQueryHandler(IUserNotificationRepository notifications) => _notifications = notifications;

    public async Task<Result<PagedResult<UserNotificationDto>>> Handle(GetNotificationFeedQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var (items, total) = await _notifications.GetFeedPageAsync(request.UserId, page, pageSize, request.UnreadOnly, cancellationToken);

        var dtos = items.Select(n => n.ToDto()).ToList();
        return Result.Success(PagedResult<UserNotificationDto>.Create(dtos, page, pageSize, total));
    }
}
