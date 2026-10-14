namespace ZIONShop.Orders.Domain.Enums;

public enum OrderStatus
{
    PendingStock,
    AwaitingPayment,
    Confirmed,
    Paid,
    Shipped,
    Completed,
    Cancelled
}
