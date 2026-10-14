using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Orders.Domain.Entities;

public class OrderIdempotencyKey : BaseEntity
{
    private OrderIdempotencyKey() { }

    public Guid UserId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public Guid OrderId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static OrderIdempotencyKey Create(Guid userId, string key, string requestHash, Guid orderId, DateTime createdAt) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Key = key,
        RequestHash = requestHash,
        OrderId = orderId,
        CreatedAt = createdAt
    };
}
