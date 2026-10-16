using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Common.Pagination;
using ZIONShop.Notifications.Application.DTOs;
using ZIONShop.Notifications.Application.Mappings;
using ZIONShop.Notifications.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Notifications.Application.Features.GetMyNotifications;

public class GetMyNotificationsQueryHandler : IRequestHandler<GetMyNotificationsQuery, Result<PagedResult<MyNotificationDto>>>
{
    private readonly INotificationLogRepository _logs;

    public GetMyNotificationsQueryHandler(INotificationLogRepository logs) => _logs = logs;

    public async Task<Result<PagedResult<MyNotificationDto>>> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        var query = _logs.Query().AsNoTracking()
            .Where(l => l.UserId == request.UserId)
            .OrderByDescending(l => l.CreatedAt);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        var dtos = items.Select(l => l.ToMyDto()).ToList();
        return Result.Success(PagedResult<MyNotificationDto>.Create(dtos, page, pageSize, total));
    }
}
