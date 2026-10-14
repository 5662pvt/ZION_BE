using ZIONShop.Orders.Domain.Enums;
using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Orders.Domain.Entities;

public class OrderStatusHistory : BaseEntity
{
    private OrderStatusHistory() { }

    public Guid OrderId { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime At { get; private set; }
    public string? Note { get; private set; }

    public static OrderStatusHistory Create(Guid orderId, OrderStatus status, string? note = null) => new()
    {
        Id = Guid.NewGuid(),
        OrderId = orderId,
        Status = status,
        At = DateTime.UtcNow,
        Note = note
    };
}
