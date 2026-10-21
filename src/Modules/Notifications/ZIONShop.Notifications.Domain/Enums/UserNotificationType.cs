namespace ZIONShop.Notifications.Domain.Enums;

public enum UserNotificationType
{
    OrderPlaced,
    OrderConfirmed,
    OrderShipped,
    OrderCancelled,
    PaymentCompleted,
    PaymentFailed,
    OrderCreatedForReview,
    OrderCompleted,
    ReturnRequested,
    ReturnApproved,
    ReturnRejected,
    ReturnShippedBack,
    ReturnRefunded
}
