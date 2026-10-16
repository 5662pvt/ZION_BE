using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Users.Domain.Entities;

public class WishlistItem : BaseEntity
{
    private WishlistItem() { }

    public Guid UserId { get; private set; }
    public Guid ProductId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static WishlistItem Create(Guid userId, Guid productId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        ProductId = productId,
        CreatedAt = DateTime.UtcNow
    };
}
