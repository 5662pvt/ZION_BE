using Microsoft.Extensions.Options;
using ZIONShop.Contracts.Orders;
using ZIONShop.Loyalty.Application.IntegrationEvents;
using ZIONShop.Loyalty.Application.Options;
using ZIONShop.Loyalty.Domain.Enums;
using ZIONShop.Loyalty.Tests.Fakes;

namespace ZIONShop.Loyalty.Tests.IntegrationEvents;

public class OrderCompletedIntegrationEventHandlerTests
{
    private static OrderDetails BuildOrder(Guid orderId, Guid userId, decimal subtotal, decimal discountAmount = 0m, string? discountTarget = null, decimal pointsDiscount = 0m) =>
        new(orderId, userId, "DH001", "Completed", "COD", subtotal, discountAmount, null, discountTarget, 0m, subtotal - discountAmount, "VND",
            "A", "0900000000", "Address", null, DateTime.UtcNow, Array.Empty<OrderDetailsLine>(), 0, pointsDiscount);

    private static OrderCompletedIntegrationEventHandler BuildHandler(FakeOrderDetailsReader reader, FakeLoyaltyMutationService mutations, int earnRatePercent = 1) =>
        new(mutations, reader, Options.Create(new LoyaltyOptions { EarnRatePercent = earnRatePercent }));

    [Fact]
    public async Task HandleAsync_Should_EarnFloorOfEligibleTimesRate_When_DiscountTargetIsItems()
    {
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reader = new FakeOrderDetailsReader().With(BuildOrder(orderId, userId, 1_000_000m, 50_000m, "Items"));
        var mutations = new FakeLoyaltyMutationService();
        var handler = BuildHandler(reader, mutations);

        await handler.HandleAsync(new OrderCompletedIntegrationEvent(orderId, userId));

        mutations.Earns.Should().ContainSingle();
        mutations.Earns[0].Points.Should().Be(9500);
        mutations.Earns[0].Source.Should().Be(PointLotSource.Order);
        mutations.Earns[0].SourceRef.Should().Be(orderId.ToString());
    }

    [Fact]
    public async Task HandleAsync_Should_NotReduceBase_When_DiscountTargetIsShipping()
    {
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reader = new FakeOrderDetailsReader().With(BuildOrder(orderId, userId, 1_000_000m, 30_000m, "Shipping"));
        var mutations = new FakeLoyaltyMutationService();
        var handler = BuildHandler(reader, mutations);

        await handler.HandleAsync(new OrderCompletedIntegrationEvent(orderId, userId));

        mutations.Earns[0].Points.Should().Be(10_000);
    }

    [Fact]
    public async Task HandleAsync_Should_SubtractPointsDiscount_From_Eligible()
    {
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reader = new FakeOrderDetailsReader().With(BuildOrder(orderId, userId, 1_000_000m, pointsDiscount: 100_000m));
        var mutations = new FakeLoyaltyMutationService();
        var handler = BuildHandler(reader, mutations);

        await handler.HandleAsync(new OrderCompletedIntegrationEvent(orderId, userId));

        mutations.Earns[0].Points.Should().Be(9_000);
    }

    [Fact]
    public async Task HandleAsync_Should_NotEarn_When_PointsWouldBeZero()
    {
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reader = new FakeOrderDetailsReader().With(BuildOrder(orderId, userId, 0m));
        var mutations = new FakeLoyaltyMutationService();
        var handler = BuildHandler(reader, mutations);

        await handler.HandleAsync(new OrderCompletedIntegrationEvent(orderId, userId));

        mutations.Earns.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_Should_NotEarn_When_OrderNotFound()
    {
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var mutations = new FakeLoyaltyMutationService();
        var handler = BuildHandler(new FakeOrderDetailsReader(), mutations);

        await handler.HandleAsync(new OrderCompletedIntegrationEvent(orderId, userId));

        mutations.Earns.Should().BeEmpty();
    }
}
