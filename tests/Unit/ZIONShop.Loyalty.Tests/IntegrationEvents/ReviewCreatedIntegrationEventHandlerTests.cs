using Microsoft.Extensions.Options;
using ZIONShop.Contracts.Reviews;
using ZIONShop.Loyalty.Application.IntegrationEvents;
using ZIONShop.Loyalty.Application.Options;
using ZIONShop.Loyalty.Domain.Enums;
using ZIONShop.Loyalty.Tests.Fakes;

namespace ZIONShop.Loyalty.Tests.IntegrationEvents;

public class ReviewCreatedIntegrationEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_EarnReviewBonus_KeyedByProductId()
    {
        var userId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var mutations = new FakeLoyaltyMutationService();
        var handler = new ReviewCreatedIntegrationEventHandler(mutations, Options.Create(new LoyaltyOptions { ReviewBonus = 100 }));

        await handler.HandleAsync(new ReviewCreatedIntegrationEvent(Guid.NewGuid(), userId, productId, Guid.NewGuid(), 5));

        mutations.Earns.Should().ContainSingle();
        var earn = mutations.Earns[0];
        earn.UserId.Should().Be(userId);
        earn.Points.Should().Be(100);
        earn.Source.Should().Be(PointLotSource.Review);
        earn.SourceRef.Should().Be(productId.ToString());
        earn.OrderId.Should().BeNull();
        earn.ProductId.Should().Be(productId);
    }
}
