using MediatR;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Notifications.Application.Features.GetUnreadNotificationCount;

public record GetUnreadNotificationCountQuery(Guid UserId) : IRequest<Result<int>>;
