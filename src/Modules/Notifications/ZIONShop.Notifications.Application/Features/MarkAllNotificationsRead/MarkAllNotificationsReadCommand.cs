using MediatR;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Notifications.Application.Features.MarkAllNotificationsRead;

public record MarkAllNotificationsReadCommand(Guid UserId) : IRequest<Result<int>>;
