using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Common.Pagination;
using ZIONShop.Notifications.Application.DTOs;
using ZIONShop.Notifications.Application.Mappings;
using ZIONShop.Notifications.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Notifications.Application.Features.GetNotificationLogs;

public class GetNotificationLogsQueryHandler : IRequestHandler<GetNotificationLogsQuery, Result<PagedResult<NotificationLogDto>>>
{
    private readonly INotificationLogRepository _logs;

    public GetNotificationLogsQueryHandler(INotificationLogRepository logs) => _logs = logs;

    public async Task<Result<PagedResult<NotificationLogDto>>> Handle(GetNotificationLogsQuery request, CancellationToken cancellationToken)
    {
        var query = _logs.Query().AsNoTracking();

        if (request.OrderId.HasValue)
            query = query.Where(l => l.OrderId == request.OrderId.Value);

        query = query.OrderByDescending(l => l.CreatedAt);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        var dtos = items.Select(l => l.ToDto()).ToList();
        return Result.Success(PagedResult<NotificationLogDto>.Create(dtos, page, pageSize, total));
    }
}
