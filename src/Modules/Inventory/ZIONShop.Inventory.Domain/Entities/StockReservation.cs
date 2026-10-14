using ZIONShop.Inventory.Domain.Enums;
using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Inventory.Domain.Entities;

public class StockReservation : BaseEntity
{
    private StockReservation() { }

    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public ReservationStatus Status { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static StockReservation Create(Guid orderId, Guid productId, int quantity, DateTime? expiresAt, DateTime now)
    {
        return new StockReservation
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductId = productId,
            Quantity = quantity,
            Status = ReservationStatus.Active,
            ExpiresAt = expiresAt,
            CreatedAt = now
        };
    }

    public void MarkCommitted() => Status = ReservationStatus.Committed;
    public void MarkReleased() => Status = ReservationStatus.Released;
    public void MarkExpired() => Status = ReservationStatus.Expired;
}
