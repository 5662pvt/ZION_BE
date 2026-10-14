using ZIONShop.Contracts.Orders;
using ZIONShop.Promotions.Application.IntegrationEvents;
using ZIONShop.Promotions.Tests.Fakes;

namespace ZIONShop.Promotions.Tests.IntegrationEvents;

public class OrderCancelledIntegrationEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_ReleaseCouponAndGifts_When_OrderCancelled()
    {
        var orderId = Guid.NewGuid();
        var coupons = new FakePromotionEvaluator();
        var gifts = new FakeGiftPromotionEvaluator();
        var handler = new OrderCancelledIntegrationEventHandler(coupons, gifts);

        await handler.HandleAsync(new OrderCancelledIntegrationEvent(orderId, Guid.NewGuid(), "Customer request"), CancellationToken.None);

        coupons.ReleasedOrderIds.Should().ContainSingle(id => id == orderId);
        gifts.ReleasedGiftsOrderIds.Should().ContainSingle(id => id == orderId);
    }
}
