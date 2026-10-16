using MediatR;
using ZIONShop.Common.Pagination;
using ZIONShop.Notifications.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Notifications.Application.Features.GetMyNotifications;

public record GetMyNotificationsQuery(Guid UserId, int Page, int PageSize) : IRequest<Result<PagedResult<MyNotificationDto>>>;
