using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.ValueObjects;

namespace ZIONShop.Orders.Tests.Domain;

public class OrderCouponTests
{
    private static Order CreateOrder() => Order.Place(
        Guid.NewGuid(),
        PaymentMethod.COD,
        "VND",
        30000,
        new ShippingAddress(Guid.NewGuid(), "Nguyen Van A", "0900000000", "Hà Nội", "Cầu Giấy", "123 Đường ABC"),
        new[] { new OrderLineInput(Guid.NewGuid(), "Widget", 100000m, 2) },
        null);

    [Fact]
    public void ApplyCoupon_Should_StoreDiscountTarget_When_TargetIsShipping()
    {
        var order = CreateOrder();
        order.ApplyCoupon("FREESHIP", 30000m, "Shipping");

        order.CouponCode.Should().Be("FREESHIP");
        order.DiscountAmount.Should().Be(30000m);
        order.DiscountTarget.Should().Be("Shipping");
    }

    [Fact]
    public void ApplyCoupon_Should_StoreDiscountTarget_When_TargetIsItems()
    {
        var order = CreateOrder();
        order.ApplyCoupon("SALE10", 20000m, "Items");

        order.DiscountTarget.Should().Be("Items");
    }

    [Fact]
    public void ApplyCoupon_Should_DefaultToItems_When_TargetIsUnrecognized()
    {
        var order = CreateOrder();
        order.ApplyCoupon("WEIRD", 20000m, "Bogus");

        order.DiscountTarget.Should().Be("Items");
    }
}
