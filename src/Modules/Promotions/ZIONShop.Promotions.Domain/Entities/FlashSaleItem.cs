using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Promotions.Domain.Entities;

public class FlashSaleItem : BaseEntity
{
    private FlashSaleItem() { }

    public Guid FlashSaleId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal FlashPrice { get; private set; }
    public int AllocatedQuantity { get; private set; }
    public int SoldQuantity { get; private set; }

    public int Available => AllocatedQuantity - SoldQuantity;

    public static FlashSaleItem Create(Guid flashSaleId, Guid productId, decimal flashPrice, int allocatedQuantity) => new()
    {
        Id = Guid.NewGuid(),
        FlashSaleId = flashSaleId,
        ProductId = productId,
        FlashPrice = flashPrice,
        AllocatedQuantity = allocatedQuantity,
        SoldQuantity = 0
    };

    /// <summary>
    /// Tracked-entity equivalent of <c>IFlashSaleItemRepository.TryReserveAsync</c>'s atomic conditional
    /// UPDATE, for callers that already hold this instance loaded (e.g. the InMemory-provider test fake,
    /// which cannot translate the real repository's ExecuteUpdateAsync). Not used by the production atomic
    /// path itself.
    /// </summary>
    public bool TryReserve(int quantity)
    {
        if (SoldQuantity + quantity > AllocatedQuantity) return false;
        SoldQuantity += quantity;
        return true;
    }

    /// <summary>Tracked-entity equivalent of <c>IFlashSaleItemRepository.ReleaseAsync</c>; floors at 0.</summary>
    public void Release(int quantity) => SoldQuantity = Math.Max(0, SoldQuantity - quantity);
}
