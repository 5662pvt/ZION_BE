using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.ValueObjects;

namespace ZIONShop.Orders.Tests.Domain;

public class OrderPointsTests
{
    private static Order CreateOrder() => Order.Place(
        Guid.NewGuid(),
        PaymentMethod.COD,
        "VND",
        30000,
        new ShippingAddress(Guid.NewGuid(), "Nguyen Van A", "0900000000", "Hà Nội", "Cầu Giấy", "123 Đường ABC"),
        new[] { new OrderLineInput(Guid.NewGuid(), "Widget", 100000m, 2) }, // subtotal 200000
        null);

    [Fact]
    public void ApplyPoints_Should_ReduceTotal_When_DiscountWithinSubtotal()
    {
        var order = CreateOrder();
        order.ApplyPoints(1000, 1000m);

        order.PointsUsed.Should().Be(1000);
        order.PointsDiscount.Should().Be(1000m);
        order.Total.Should().Be(229000m); // 200000 - 0 - 1000 + 30000
    }

    [Fact]
    public void ApplyPoints_Should_ClampDiscount_When_DiscountExceedsSubtotalMinusItemsCoupon()
    {
        var order = CreateOrder();
        order.ApplyCoupon("SALE10", 20000m, "Items"); // cap becomes 200000 - 20000 = 180000

        order.ApplyPoints(500_000, 999_999m);

        order.PointsDiscount.Should().Be(180000m);
        order.Total.Should().Be(30000m); // 200000 - 20000 - 180000 + 30000
    }

    [Fact]
    public void ApplyPoints_Should_NotShrinkCap_When_CouponTargetsShipping()
    {
        var order = CreateOrder();
        order.ApplyCoupon("FREESHIP", 30000m, "Shipping");

        order.ApplyPoints(1_000_000, 999_999m);

        order.PointsDiscount.Should().Be(200000m); // cap = full Subtotal, shipping coupon doesn't reduce it
    }

    [Fact]
    public void ApplyPoints_Should_ClampToZero_When_DiscountNegative()
    {
        var order = CreateOrder();
        order.ApplyPoints(0, -500m);

        order.PointsUsed.Should().Be(0);
        order.PointsDiscount.Should().Be(0m);
    }

    [Fact]
    public void ApplyPoints_Should_ClampPointsToZero_When_PointsNegative()
    {
        var order = CreateOrder();
        order.ApplyPoints(-100, 0m);

        order.PointsUsed.Should().Be(0);
    }
}
