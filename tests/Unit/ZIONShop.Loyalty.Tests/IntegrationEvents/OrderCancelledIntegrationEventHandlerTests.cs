using ZIONShop.Contracts.Orders;
using ZIONShop.Loyalty.Application.IntegrationEvents;
using ZIONShop.Loyalty.Tests.Fakes;

namespace ZIONShop.Loyalty.Tests.IntegrationEvents;

public class OrderCancelledIntegrationEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_ReleasePoints_When_OrderCancelled()
    {
        var orderId = Guid.NewGuid();
        var loyalty = new FakeLoyaltyPointsService();
        var handler = new OrderCancelledIntegrationEventHandler(loyalty);

        await handler.HandleAsync(new OrderCancelledIntegrationEvent(orderId, Guid.NewGuid(), "Customer request"));

        loyalty.ReleasedOrderIds.Should().ContainSingle(id => id == orderId);
    }
}
