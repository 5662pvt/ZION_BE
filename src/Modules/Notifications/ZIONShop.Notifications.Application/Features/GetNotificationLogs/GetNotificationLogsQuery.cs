using MediatR;
using ZIONShop.Common.Pagination;
using ZIONShop.Notifications.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Notifications.Application.Features.GetNotificationLogs;

public record GetNotificationLogsQuery(int Page, int PageSize, Guid? OrderId) : IRequest<Result<PagedResult<NotificationLogDto>>>;
