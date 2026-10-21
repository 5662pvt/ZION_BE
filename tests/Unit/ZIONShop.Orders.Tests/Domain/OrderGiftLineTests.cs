using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.ValueObjects;

namespace ZIONShop.Orders.Tests.Domain;

public class OrderGiftLineTests
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
    public void AddGiftLine_Should_AddFreeLine_When_PendingStock()
    {
        var order = CreateOrder();
        var subtotalBefore = order.Subtotal;
        var productId = Guid.NewGuid();
        var promotionId = Guid.NewGuid();

        var result = order.AddGiftLine(productId, "Nón lưỡi trai", 1, promotionId);

        result.IsSuccess.Should().BeTrue();
        var giftLine = order.Lines.Should().ContainSingle(l => l.ProductId == productId).Subject;
        giftLine.IsGift.Should().BeTrue();
        giftLine.UnitPrice.Should().Be(0m);
        giftLine.LineTotal.Should().Be(0m);
        giftLine.GiftPromotionId.Should().Be(promotionId);
        order.Subtotal.Should().Be(subtotalBefore); // gift lines never affect Subtotal
    }

    [Fact]
    public void AddGiftLine_Should_Fail_When_NotPendingStock()
    {
        var order = CreateOrder();
        order.ConfirmReservation();

        var result = order.AddGiftLine(Guid.NewGuid(), "Nón lưỡi trai", 1, Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        order.Lines.Should().NotContain(l => l.IsGift);
    }

    [Fact]
    public void DropGiftLines_Should_RemoveMatchingGiftLines_And_AddStatusHistoryNote()
    {
        var order = CreateOrder();
        var promotionId = Guid.NewGuid();
        var giftProductId = Guid.NewGuid();
        order.AddGiftLine(giftProductId, "Nón lưỡi trai", 1, promotionId);
        var historyCountBefore = order.StatusHistory.Count;

        var dropped = order.DropGiftLines(new[] { giftProductId });

        dropped.Should().ContainSingle(l => l.ProductId == giftProductId);
        order.Lines.Should().NotContain(l => l.IsGift);
        order.StatusHistory.Should().HaveCount(historyCountBefore + 1);
        order.StatusHistory.Should().Contain(h => h.Note == "Quà tặng hết hàng – đã bỏ quà");
    }

    [Fact]
    public void DropGiftLines_Should_NotTouchPurchasedLines()
    {
        var order = CreateOrder();
        var purchasedProductId = order.Lines.Single().ProductId;

        var dropped = order.DropGiftLines(new[] { purchasedProductId });

        dropped.Should().BeEmpty();
        order.Lines.Should().ContainSingle(l => l.ProductId == purchasedProductId);
    }

    [Fact]
    public void DropGiftLines_Should_ReturnEmpty_And_NotAddHistory_When_NothingMatches()
    {
        var order = CreateOrder();
        var historyCountBefore = order.StatusHistory.Count;

        var dropped = order.DropGiftLines(new[] { Guid.NewGuid() });

        dropped.Should().BeEmpty();
        order.StatusHistory.Should().HaveCount(historyCountBefore);
    }

    [Fact]
    public void AddGiftLine_Should_StampGrantedVariant_When_GiftIsASpecificVariant()
    {
        var order = CreateOrder();
        var giftProductId = Guid.NewGuid();
        var giftVariantId = Guid.NewGuid();
        var promotionId = Guid.NewGuid();

        var result = order.AddGiftLine(giftProductId, "Nón lưỡi trai", 1, promotionId, giftVariantId, "Đen", "HAT-BLK");

        result.IsSuccess.Should().BeTrue();
        var giftLine = order.Lines.Should().ContainSingle(l => l.IsGift).Subject;
        giftLine.VariantId.Should().Be(giftVariantId);
        giftLine.VariantLabel.Should().Be("Đen");
        giftLine.Sku.Should().Be("HAT-BLK");
    }

    [Fact]
    public void DropGiftLines_Should_KeyOffVariantId_Not_ProductId()
    {
        var order = CreateOrder();
        var sharedProductId = Guid.NewGuid();
        var giftVariantA = Guid.NewGuid();
        var giftVariantB = Guid.NewGuid();
        var promotionId = Guid.NewGuid();
        order.AddGiftLine(sharedProductId, "Áo thun", 1, promotionId, giftVariantA, "Đỏ");
        order.AddGiftLine(sharedProductId, "Áo thun", 1, promotionId, giftVariantB, "Xanh");

        var dropped = order.DropGiftLines(new[] { giftVariantA });

        dropped.Should().ContainSingle(l => l.VariantId == giftVariantA);
        order.Lines.Should().ContainSingle(l => l.IsGift && l.VariantId == giftVariantB);
    }
}
