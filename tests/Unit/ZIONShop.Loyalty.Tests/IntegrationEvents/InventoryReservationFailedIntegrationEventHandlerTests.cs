using ZIONShop.Contracts.Inventory;
using ZIONShop.Loyalty.Application.IntegrationEvents;
using ZIONShop.Loyalty.Tests.Fakes;

namespace ZIONShop.Loyalty.Tests.IntegrationEvents;

public class InventoryReservationFailedIntegrationEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_ReleasePoints_When_ReservationFailed()
    {
        var orderId = Guid.NewGuid();
        var loyalty = new FakeLoyaltyPointsService();
        var handler = new InventoryReservationFailedIntegrationEventHandler(loyalty);

        await handler.HandleAsync(new InventoryReservationFailedIntegrationEvent(orderId, "Out of stock", Array.Empty<Guid>()));

        loyalty.ReleasedOrderIds.Should().ContainSingle(id => id == orderId);
    }
}
