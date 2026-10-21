using ZIONShop.Contracts.Orders;
using ZIONShop.Notifications.Application.Templates;

namespace ZIONShop.Notifications.Tests.Templates;

public class OrderEmailTemplatesTests
{
    private static OrderDetails BuildOrder(decimal discount = 0, string? couponCode = null, string? cancelReason = null, string? discountTarget = null) => new(
        Guid.NewGuid(), Guid.NewGuid(), "ZS0001", "Confirmed", "COD",
        200000m, discount, couponCode, discountTarget, 30000m, 230000m - discount, "VND",
        "<b>Nguyen Van A</b>", "0900000000", "123 <script>alert(1)</script>", cancelReason, DateTime.UtcNow,
        new[] { new OrderDetailsLine("Widget <3", 2, 100000m, 200000m) });

    [Fact]
    public void BuildConfirmedBody_Should_ContainCodeAndFormattedTotal()
    {
        var order = BuildOrder();
        var body = OrderEmailTemplates.BuildConfirmedBody(null, order, "http://localhost:3000");

        body.Should().Contain(order.Code);
        body.Should().Contain("230.000"); // vi-VN grouped total
        body.Should().Contain($"/orders/{order.OrderId}");
    }

    [Fact]
    public void BuildConfirmedBody_Should_ContainDiscountAndCouponCode_When_DiscountApplied()
    {
        var order = BuildOrder(discount: 20000m, couponCode: "SALE10");
        var body = OrderEmailTemplates.BuildConfirmedBody(null, order, "http://localhost:3000");

        body.Should().Contain("SALE10");
        body.Should().Contain("20.000");
        body.Should().Contain("Giảm giá (SALE10)");
    }

    [Fact]
    public void BuildConfirmedBody_Should_LabelAsShippingDiscount_When_DiscountTargetIsShipping()
    {
        var order = BuildOrder(discount: 30000m, couponCode: "FREESHIP", discountTarget: "Shipping");
        var body = OrderEmailTemplates.BuildConfirmedBody(null, order, "http://localhost:3000");

        body.Should().Contain("Giảm phí vận chuyển (FREESHIP)");
        body.Should().NotContain("Giảm giá (FREESHIP)");
    }

    [Fact]
    public void BuildConfirmedBody_Should_NotContainDiscountRow_When_NoDiscount()
    {
        var order = BuildOrder();
        var body = OrderEmailTemplates.BuildConfirmedBody(null, order, "http://localhost:3000");

        body.Should().NotContain("Giảm giá");
    }

    [Fact]
    public void BuildConfirmedBody_Should_HtmlEncodeDynamicValues()
    {
        var order = BuildOrder();
        var body = OrderEmailTemplates.BuildConfirmedBody(null, order, "http://localhost:3000");

        body.Should().NotContain("<script>alert(1)</script>");
        body.Should().NotContain("<b>Nguyen Van A</b>");
        body.Should().Contain("&lt;script&gt;");
        body.Should().Contain("Widget &lt;3");
    }

    [Fact]
    public void BuildConfirmedBody_Should_KeepVietnameseLettersLiteral()
    {
        var order = BuildOrder();
        var body = OrderEmailTemplates.BuildConfirmedBody("Khách E2E", order, "http://localhost:3000");

        body.Should().Contain("Xin chào Khách E2E");
        body.Should().NotContain("&#");
    }

    [Fact]
    public void BuildConfirmedBody_Should_RenderGiftLine_With_FreeLabelAndSuffix()
    {
        var order = new OrderDetails(
            Guid.NewGuid(), Guid.NewGuid(), "ZS0001", "Confirmed", "COD",
            200000m, 0, null, null, 30000m, 230000m, "VND",
            "Nguyen Van A", "0900000000", "123 ABC", null, DateTime.UtcNow,
            new[]
            {
                new OrderDetailsLine("Widget", 2, 100000m, 200000m),
                new OrderDetailsLine("Nón lưỡi trai", 1, 0m, 0m, IsGift: true)
            });

        var body = OrderEmailTemplates.BuildConfirmedBody(null, order, "http://localhost:3000");

        body.Should().Contain("Nón lưỡi trai (Quà tặng)");
        body.Should().Contain("Miễn phí");
    }

    [Fact]
    public void BuildConfirmedBody_Should_RenderVariantLabel_When_LineHasVariantLabel()
    {
        var order = new OrderDetails(
            Guid.NewGuid(), Guid.NewGuid(), "ZS0001", "Confirmed", "COD",
            100000m, 0, null, null, 30000m, 130000m, "VND",
            "Nguyen Van A", "0900000000", "123 ABC", null, DateTime.UtcNow,
            new[] { new OrderDetailsLine("Áo thun", 1, 100000m, 100000m, VariantLabel: "Đỏ, Size M") });

        var body = OrderEmailTemplates.BuildConfirmedBody(null, order, "http://localhost:3000");

        body.Should().Contain("Áo thun (Đỏ, Size M)");
    }

    [Fact]
    public void BuildCancelledBody_Should_ContainReason_When_Provided()
    {
        var order = BuildOrder(cancelReason: "Khách hàng đổi ý <script>");
        var body = OrderEmailTemplates.BuildCancelledBody(null, order, "http://localhost:3000");

        body.Should().Contain("Khách hàng đổi ý");
        body.Should().NotContain("<script>");
        body.Should().Contain("&lt;script&gt;");
    }
}
