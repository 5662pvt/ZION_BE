using Moq;
using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Promotions;
using ZIONShop.Promotions.Application.IntegrationEvents;

namespace ZIONShop.Promotions.Tests.IntegrationEvents;

public class OrderCancelledIntegrationEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_DelegateToEvaluatorRelease_When_Called()
    {
        var orderId = Guid.NewGuid();
        var evaluator = new Mock<IPromotionEvaluator>();
        evaluator.Setup(e => e.ReleaseAsync(orderId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var handler = new OrderCancelledIntegrationEventHandler(evaluator.Object);

        await handler.HandleAsync(new OrderCancelledIntegrationEvent(orderId, Guid.NewGuid(), "Khách hủy đơn"));

        evaluator.Verify(e => e.ReleaseAsync(orderId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
