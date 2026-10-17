using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Notifications.Domain.Exceptions;

public static class NotificationsErrors
{
    public static readonly Error UserNotificationNotFound = Error.NotFound("Notifications.UserNotificationNotFound", "Notification not found");
}
