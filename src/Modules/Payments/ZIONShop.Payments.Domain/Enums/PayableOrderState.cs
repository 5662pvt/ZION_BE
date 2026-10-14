namespace ZIONShop.Payments.Domain.Enums;

public enum PayableOrderState
{
    AwaitingStock,
    Payable,
    Paid,
    Cancelled
}
