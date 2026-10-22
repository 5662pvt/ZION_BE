using ZIONShop.Contracts.Orders;
using ZIONShop.Loyalty.Application.IntegrationEvents;
using ZIONShop.Loyalty.Tests.Fakes;

namespace ZIONShop.Loyalty.Tests.IntegrationEvents;

public class OrderRefundedIntegrationEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_RevokeEarnedPoints_AndRefundSpentPoints()
    {
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var mutations = new FakeLoyaltyMutationService();
        var handler = new OrderRefundedIntegrationEventHandler(mutations, new FakeOrderDetailsReader());

        await handler.HandleAsync(new OrderRefundedIntegrationEvent(orderId, userId, Guid.NewGuid(), 50_000m, "Cod"));

        mutations.Revocations.Should().ContainSingle(r => r.OrderId == orderId && r.UserId == userId);
        mutations.Refunds.Should().ContainSingle(r => r.OrderId == orderId);
    }
}
