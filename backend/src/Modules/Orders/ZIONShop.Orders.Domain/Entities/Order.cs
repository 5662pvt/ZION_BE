using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.Exceptions;
using ZIONShop.Orders.Domain.ValueObjects;
using ZIONShop.SharedKernel.Entities;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Domain.Entities;

public class Order : AggregateRoot
{
    private const string CodeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private readonly List<OrderLine> _lines = new();
    private readonly List<OrderStatusHistory> _statusHistory = new();

    private Order() { }

    public string Code { get; private set; } = string.Empty;
    public Guid UserId { get; private set; }
    public OrderStatus Status { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public string Currency { get; private set; } = "VND";
    public decimal Subtotal { get; private set; }
    public decimal ShippingFee { get; private set; }
    public string? CouponCode { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public string? DiscountTarget { get; private set; }
    public decimal Total => Subtotal - DiscountAmount + ShippingFee;
    public string? Note { get; private set; }
    public string? CancelReason { get; private set; }
    public ShippingAddress ShippingAddress { get; private set; } = default!;

    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();
    public IReadOnlyCollection<OrderStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    public static Order Place(
        Guid userId,
        PaymentMethod paymentMethod,
        string currency,
        decimal shippingFee,
        ShippingAddress shippingAddress,
        IReadOnlyList<(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity)> lines,
        string? note)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            Code = GenerateCode(),
            UserId = userId,
            PaymentMethod = paymentMethod,
            Currency = currency,
            ShippingFee = shippingFee,
            ShippingAddress = shippingAddress,
            Note = note,
            Status = OrderStatus.PendingStock
        };

        foreach (var line in lines)
            order._lines.Add(OrderLine.Create(order.Id, line.ProductId, line.ProductName, line.UnitPrice, line.Quantity));

        order.Subtotal = order._lines.Sum(l => l.LineTotal);
        order._statusHistory.Add(OrderStatusHistory.Create(order.Id, OrderStatus.PendingStock));
        return order;
    }

    private static string GenerateCode()
    {
        var suffix = new string(Enumerable.Range(0, 4)
            .Select(_ => CodeAlphabet[Random.Shared.Next(CodeAlphabet.Length)])
            .ToArray());
        return $"ZS{DateTime.UtcNow:yyMMdd}{suffix}";
    }

    public void ApplyCoupon(string code, decimal discountAmount, string discountTarget)
    {
        CouponCode = code;
        DiscountAmount = Math.Clamp(discountAmount, 0m, Subtotal);
        DiscountTarget = discountTarget is "Items" or "Shipping" ? discountTarget : "Items";
    }

    public Result ConfirmReservation()
    {
        if (Status != OrderStatus.PendingStock) return Result.Failure(OrdersErrors.InvalidTransition);
        Status = PaymentMethod == PaymentMethod.COD ? OrderStatus.Confirmed : OrderStatus.AwaitingPayment;
        _statusHistory.Add(OrderStatusHistory.Create(Id, Status));
        return Result.Success();
    }

    public Result FailReservation(string reason)
    {
        if (Status != OrderStatus.PendingStock) return Result.Failure(OrdersErrors.InvalidTransition);
        Status = OrderStatus.Cancelled;
        CancelReason = reason;
        _statusHistory.Add(OrderStatusHistory.Create(Id, Status, reason));
        return Result.Success();
    }

    public Result ExpireReservation()
    {
        if (Status != OrderStatus.AwaitingPayment) return Result.Failure(OrdersErrors.InvalidTransition);
        CancelReason = "Quá hạn thanh toán";
        Status = OrderStatus.Cancelled;
        _statusHistory.Add(OrderStatusHistory.Create(Id, Status, CancelReason));
        return Result.Success();
    }

    public Result CompletePayment()
    {
        if (Status != OrderStatus.AwaitingPayment) return Result.Failure(OrdersErrors.InvalidTransition);
        Status = OrderStatus.Paid;
        _statusHistory.Add(OrderStatusHistory.Create(Id, Status));
        return Result.Success();
    }

    public Result FailPayment()
    {
        if (Status != OrderStatus.AwaitingPayment) return Result.Failure(OrdersErrors.InvalidTransition);
        CancelReason = "Thanh toán thất bại";
        Status = OrderStatus.Cancelled;
        _statusHistory.Add(OrderStatusHistory.Create(Id, Status, CancelReason));
        return Result.Success();
    }

    private const int CancelReasonMaxLength = 500;

    /// <summary>Owner can cancel up to Confirmed; staff can also cancel an already-Paid order (any time before Shipped).</summary>
    public Result Cancel(string? reason, bool isStaff)
    {
        var allowed = isStaff
            ? Status is OrderStatus.PendingStock or OrderStatus.AwaitingPayment or OrderStatus.Confirmed or OrderStatus.Paid
            : Status is OrderStatus.PendingStock or OrderStatus.AwaitingPayment or OrderStatus.Confirmed;
        if (!allowed) return Result.Failure(OrdersErrors.CancelNotAllowed);

        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            trimmed = isStaff ? "Cửa hàng hủy đơn" : "Khách hủy đơn";
        else if (trimmed.Length > CancelReasonMaxLength)
            trimmed = trimmed[..CancelReasonMaxLength];

        Status = OrderStatus.Cancelled;
        CancelReason = trimmed;
        _statusHistory.Add(OrderStatusHistory.Create(Id, Status, trimmed));
        return Result.Success();
    }

    public Result Ship()
    {
        if (Status is not (OrderStatus.Confirmed or OrderStatus.Paid)) return Result.Failure(OrdersErrors.InvalidTransition);
        Status = OrderStatus.Shipped;
        _statusHistory.Add(OrderStatusHistory.Create(Id, Status));
        return Result.Success();
    }

    public Result Complete()
    {
        if (Status != OrderStatus.Shipped) return Result.Failure(OrdersErrors.InvalidTransition);
        Status = OrderStatus.Completed;
        _statusHistory.Add(OrderStatusHistory.Create(Id, Status));
        return Result.Success();
    }
}
