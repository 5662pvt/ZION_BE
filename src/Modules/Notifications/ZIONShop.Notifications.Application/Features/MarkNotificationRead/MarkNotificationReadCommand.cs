using MediatR;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Notifications.Application.Features.MarkNotificationRead;

public record MarkNotificationReadCommand(Guid Id, Guid UserId) : IRequest<Result>;
