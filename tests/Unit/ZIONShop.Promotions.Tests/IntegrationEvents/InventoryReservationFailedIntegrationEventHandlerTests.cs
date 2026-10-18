using ZIONShop.Contracts.Inventory;
using ZIONShop.Promotions.Application.IntegrationEvents;
using ZIONShop.Promotions.Tests.Fakes;

namespace ZIONShop.Promotions.Tests.IntegrationEvents;

public class InventoryReservationFailedIntegrationEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_ReleaseCouponAndGiftsAndFlashSale_When_InventoryReservationFailed()
    {
        var orderId = Guid.NewGuid();
        var coupons = new FakePromotionEvaluator();
        var gifts = new FakeGiftPromotionEvaluator();
        var flashSales = new FakeFlashSaleEvaluator();
        var handler = new InventoryReservationFailedIntegrationEventHandler(coupons, gifts, flashSales);

        await handler.HandleAsync(new InventoryReservationFailedIntegrationEvent(orderId, "Out of stock", new List<Guid>()), CancellationToken.None);

        coupons.ReleasedOrderIds.Should().ContainSingle(id => id == orderId);
        gifts.ReleasedGiftsOrderIds.Should().ContainSingle(id => id == orderId);
        flashSales.ReleasedOrderIds.Should().ContainSingle(id => id == orderId);
    }
}
