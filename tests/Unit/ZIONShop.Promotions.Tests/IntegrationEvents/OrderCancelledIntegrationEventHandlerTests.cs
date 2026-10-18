using ZIONShop.Contracts.Orders;
using ZIONShop.Promotions.Application.IntegrationEvents;
using ZIONShop.Promotions.Tests.Fakes;

namespace ZIONShop.Promotions.Tests.IntegrationEvents;

public class OrderCancelledIntegrationEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_ReleaseCouponAndGiftsAndFlashSale_When_OrderCancelled()
    {
        var orderId = Guid.NewGuid();
        var coupons = new FakePromotionEvaluator();
        var gifts = new FakeGiftPromotionEvaluator();
        var flashSales = new FakeFlashSaleEvaluator();
        var handler = new OrderCancelledIntegrationEventHandler(coupons, gifts, flashSales);

        await handler.HandleAsync(new OrderCancelledIntegrationEvent(orderId, Guid.NewGuid(), "Customer request"), CancellationToken.None);

        coupons.ReleasedOrderIds.Should().ContainSingle(id => id == orderId);
        gifts.ReleasedGiftsOrderIds.Should().ContainSingle(id => id == orderId);
        flashSales.ReleasedOrderIds.Should().ContainSingle(id => id == orderId);
    }
}
