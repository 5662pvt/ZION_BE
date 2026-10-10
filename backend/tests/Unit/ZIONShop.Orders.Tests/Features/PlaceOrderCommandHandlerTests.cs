using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZIONShop.Contracts.Cart;
using ZIONShop.Orders.Application.Features.PlaceOrder;
using ZIONShop.Orders.Application.Options;
using ZIONShop.Orders.Infrastructure.Persistence;
using ZIONShop.Orders.Infrastructure.Repositories;
using ZIONShop.Orders.Tests.Fakes;

namespace ZIONShop.Orders.Tests.Features;

public class PlaceOrderCommandHandlerTests
{
    private static (OrdersDbContext db, OrderRepository orders) BuildScope()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new OrdersDbContext(options);
        return (db, new OrderRepository(db));
    }

    private static IOptions<CheckoutOptions> DefaultCheckout() =>
        Options.Create(new CheckoutOptions { ShippingFee = 30000, FreeShippingThreshold = 500000, Currency = "VND" });

    [Fact]
    public async Task Handle_Should_PlaceOrder_When_CartAndAddressValid()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 2, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), DefaultCheckout());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", "Giao giờ hành chính"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Subtotal.Should().Be(200000m);
        result.Value.ShippingFee.Should().Be(30000m); // below free-shipping threshold
        result.Value.Total.Should().Be(230000m);
        result.Value.Status.Should().Be("PendingStock");
        result.Value.Lines.Should().ContainSingle(l => l.ProductId == productId && l.Quantity == 2);
        result.Value.Code.Should().StartWith("ZS");
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_CartEmpty()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var handler = new PlaceOrderCommandHandler(
            orders, db, new FakeCartReader(), new FakeUserAddressReader(), new FakeProductPriceLookup(), new FakePromotionEvaluator(), DefaultCheckout());

        var result = await handler.Handle(new PlaceOrderCommand(userId, Guid.NewGuid(), "COD", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.CartEmpty");
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_AddressNotFound()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 100000m));
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);

        var handler = new PlaceOrderCommandHandler(orders, db, cart, new FakeUserAddressReader(), prices, new FakePromotionEvaluator(), DefaultCheckout());
        var result = await handler.Handle(new PlaceOrderCommand(userId, Guid.NewGuid(), "COD", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.AddressNotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_ProductUnavailable()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m, isAvailable: false);

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), DefaultCheckout());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.ProductUnavailable");
    }

    [Fact]
    public async Task Handle_Should_WaiveShippingFee_When_SubtotalMeetsFreeShippingThreshold()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 5, 100000m)); // 500000 subtotal
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), DefaultCheckout());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Subtotal.Should().Be(500000m);
        result.Value.ShippingFee.Should().Be(0m);
        result.Value.Total.Should().Be(500000m);
    }

    [Fact]
    public async Task Handle_Should_UseFreshPrice_When_CartSnapshotPriceIsStale()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        // Cart line carries a stale price; PlaceOrder must re-read from IProductPriceLookup, not trust the cart.
        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 10m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 150000m);

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), DefaultCheckout());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "Online", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Lines.Single().UnitPrice.Should().Be(150000m);
        result.Value.Subtotal.Should().Be(150000m);
    }

    [Fact]
    public async Task Handle_Should_ApplyDiscount_When_CouponValid()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 2, 100000m)); // subtotal 200000
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);
        var promotions = new FakePromotionEvaluator().WithQuote("SALE10", 20000m, "Giảm 10%");

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, promotions, DefaultCheckout());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null, "SALE10"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Subtotal.Should().Be(200000m);
        result.Value.DiscountAmount.Should().Be(20000m);
        result.Value.CouponCode.Should().Be("SALE10");
        result.Value.ShippingFee.Should().Be(30000m); // below free-shipping threshold, computed from pre-discount subtotal
        result.Value.Total.Should().Be(210000m); // 200000 - 20000 + 30000

        promotions.ReserveCalls.Should().ContainSingle(c => c.Code == "SALE10" && c.UserId == userId && c.Subtotal == 200000m);
    }

    [Fact]
    public async Task Handle_Should_WaiveShippingFromPreDiscountSubtotal_When_CouponBringsPostDiscountBelowThreshold()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 5, 100000m)); // subtotal 500000, meets threshold
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);
        var promotions = new FakePromotionEvaluator().WithQuote("BIG", 50000m);

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, promotions, DefaultCheckout());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null, "BIG"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ShippingFee.Should().Be(0m); // still free: pre-discount subtotal (500000) met the threshold
        result.Value.Total.Should().Be(450000m); // 500000 - 50000 + 0
    }

    [Fact]
    public async Task Handle_Should_ReturnFailureWithoutPersisting_When_CouponReserveFails()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);
        var promotions = new FakePromotionEvaluator().WithError("EXPIRED", "Mã giảm giá đã hết hạn");

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, promotions, DefaultCheckout());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null, "EXPIRED"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.InvalidCoupon");
        result.Error.Message.Should().Be("Mã giảm giá đã hết hạn");
        (await db.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_ReleaseReservation_When_SaveFailsAfterReserve()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);
        var promotions = new FakePromotionEvaluator().WithQuote("SALE10", 10000m);

        var handler = new PlaceOrderCommandHandler(orders, new FakeFailingUnitOfWork(), cart, addresses, prices, promotions, DefaultCheckout());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null, "SALE10"), CancellationToken.None));

        promotions.ReleasedOrderIds.Should().HaveCount(1);
        promotions.ReserveCalls.Should().ContainSingle(c => c.OrderId == promotions.ReleasedOrderIds[0]);
    }

    [Fact]
    public async Task Handle_Should_NotCallEvaluator_When_NoCouponCode()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);
        var promotions = new FakePromotionEvaluator();

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, promotions, DefaultCheckout());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.DiscountAmount.Should().Be(0m);
        result.Value.CouponCode.Should().BeNull();
        promotions.ReserveCalls.Should().BeEmpty();
    }
}
