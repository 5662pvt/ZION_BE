using MediatR;
using ZIONShop.Common.Pagination;
using ZIONShop.Notifications.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Notifications.Application.Features.GetNotificationFeed;

public record GetNotificationFeedQuery(Guid UserId, int Page, int PageSize, bool UnreadOnly) : IRequest<Result<PagedResult<UserNotificationDto>>>;
