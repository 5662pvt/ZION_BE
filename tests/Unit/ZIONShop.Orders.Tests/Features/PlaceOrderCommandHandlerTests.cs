using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZIONShop.Contracts.Cart;
using ZIONShop.Contracts.Promotions;
using ZIONShop.Orders.Application.Features.PlaceOrder;
using ZIONShop.Orders.Application.Options;
using ZIONShop.Orders.Application.Services;
using ZIONShop.Orders.Infrastructure.Persistence;
using ZIONShop.Orders.Infrastructure.Repositories;
using ZIONShop.Orders.Tests.Fakes;

namespace ZIONShop.Orders.Tests.Features;

/// <summary>
/// No-op fake confined to this file (the only file this test project's ALLOW scope permits changing for
/// the flash-sale wiring): every existing PlaceOrder test exercises the no-flash-sale path, so Quote always
/// returns empty and nothing is ever reserved/released here. The flash-sale pricing/fallback/release
/// behavior itself is covered by FlashSaleEvaluatorTests and PlaceOrderCommandHandler's new flash-sale
/// tests below, which construct their own inline fakes.
/// </summary>
file class FakeFlashSaleEvaluator : IFlashSaleEvaluator
{
    public List<Guid> ReleasedOrderIds { get; } = new();
    public List<(IReadOnlyList<FlashSaleLine> Lines, Guid OrderId, Guid UserId)> ReserveCalls { get; } = new();

    public IReadOnlyList<FlashSaleQuote> NextQuotes { get; set; } = Array.Empty<FlashSaleQuote>();
    public IReadOnlyList<FlashSaleQuote> NextReserveResult { get; set; } = Array.Empty<FlashSaleQuote>();

    public Task<IReadOnlyList<FlashSaleQuote>> QuoteAsync(IReadOnlyList<FlashSaleLine> lines, CancellationToken ct = default) =>
        Task.FromResult(NextQuotes);

    public Task<IReadOnlyList<FlashSaleQuote>> ReserveAsync(IReadOnlyList<FlashSaleLine> lines, Guid orderId, Guid userId, CancellationToken ct = default)
    {
        ReserveCalls.Add((lines, orderId, userId));
        return Task.FromResult(NextReserveResult.Count > 0 ? NextReserveResult : NextQuotes);
    }

    public Task ReleaseAsync(Guid orderId, CancellationToken ct = default)
    {
        ReleasedOrderIds.Add(orderId);
        return Task.CompletedTask;
    }
}

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

    private static ShippingFeeCalculator DefaultShippingFeeCalculator() => new(DefaultCheckout());

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
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 2);

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", "Giao giờ hành chính"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Replayed.Should().BeFalse();
        result.Value.Order.Subtotal.Should().Be(200000m);
        result.Value.Order.ShippingFee.Should().Be(30000m); // below free-shipping threshold
        result.Value.Order.Total.Should().Be(230000m);
        result.Value.Order.Status.Should().Be("PendingStock");
        result.Value.Order.Lines.Should().ContainSingle(l => l.ProductId == productId && l.Quantity == 2);
        result.Value.Order.Code.Should().StartWith("ZS");
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_CartEmpty()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var handler = new PlaceOrderCommandHandler(
            orders, db, new FakeCartReader(), new FakeUserAddressReader(), new FakeProductPriceLookup(), new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), new FakeStockAvailabilityReader(), DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());

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

        var handler = new PlaceOrderCommandHandler(orders, db, cart, new FakeUserAddressReader(), prices, new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), new FakeStockAvailabilityReader(), DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
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

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), new FakeStockAvailabilityReader(), DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.ProductUnavailable");
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_StockInsufficient_ForPaidLine()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 3, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 2); // only 2 available, 3 requested

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.InsufficientStock");
        result.Error.Message.Should().Contain("Widget");
        (await db.Orders.CountAsync()).Should().Be(0);
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
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 5);

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Order.Subtotal.Should().Be(500000m);
        result.Value.Order.ShippingFee.Should().Be(0m);
        result.Value.Order.Total.Should().Be(500000m);
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
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 1);

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "Online", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Order.Lines.Single().UnitPrice.Should().Be(150000m);
        result.Value.Order.Subtotal.Should().Be(150000m);
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
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 2);

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, promotions, new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null, "SALE10"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Order.Subtotal.Should().Be(200000m);
        result.Value.Order.DiscountAmount.Should().Be(20000m);
        result.Value.Order.CouponCode.Should().Be("SALE10");
        result.Value.Order.DiscountTarget.Should().Be("Items");
        result.Value.Order.ShippingFee.Should().Be(30000m); // below free-shipping threshold, computed from pre-discount subtotal
        result.Value.Order.Total.Should().Be(210000m); // 200000 - 20000 + 30000

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
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 5);

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, promotions, new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null, "BIG"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Order.ShippingFee.Should().Be(0m); // still free: pre-discount subtotal (500000) met the threshold
        result.Value.Order.Total.Should().Be(450000m); // 500000 - 50000 + 0
    }

    [Fact]
    public async Task Handle_Should_StoreShippingDiscountTarget_When_CouponIsFreeShipping()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 199000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 199000m);
        var promotions = new FakePromotionEvaluator().WithQuote("FREESHIP", 30000m, discountTarget: "Shipping");
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 1);

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, promotions, new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null, "FREESHIP"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Order.DiscountTarget.Should().Be("Shipping");
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
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 1);

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, promotions, new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
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
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 1);

        var handler = new PlaceOrderCommandHandler(orders, new FakeFailingUnitOfWork(), cart, addresses, prices, promotions, new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());

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
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 1);

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, promotions, new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Order.DiscountAmount.Should().Be(0m);
        result.Value.Order.CouponCode.Should().BeNull();
        promotions.ReserveCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_AddFreeGiftLine_When_GiftPromotionGrantsAndStockAvailable()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var giftProductId = Guid.NewGuid();
        var promotionId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 2, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m).Add(giftProductId, "Nón lưỡi trai", 50000m);
        var giftEvaluator = new FakeGiftPromotionEvaluator().WithGrant(promotionId, giftProductId, 1, "Mua 2 áo tặng nón");
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 2).WithAvailable(giftProductId, 5);

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), giftEvaluator, new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Order.Subtotal.Should().Be(200000m); // gift line never affects Subtotal
        var giftLine = result.Value.Order.Lines.Should().ContainSingle(l => l.IsGift).Subject;
        giftLine.ProductId.Should().Be(giftProductId);
        giftLine.UnitPrice.Should().Be(0m);
        giftLine.LineTotal.Should().Be(0m);
        giftLine.Quantity.Should().Be(1);
        giftEvaluator.ReserveCalls.Should().ContainSingle(c => c.OrderId == result.Value.Order.Id && c.PromotionIds.Contains(promotionId));
    }

    [Fact]
    public async Task Handle_Should_DropGift_When_StockInsufficient_ForSameProductPurchasedAndGift()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var promotionId = Guid.NewGuid();

        // Purchased qty 2 + gift qty 5 of the SAME product needs 7 available, but only 6 are in stock.
        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 2, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);
        var giftEvaluator = new FakeGiftPromotionEvaluator().WithGrant(promotionId, productId, 5);
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 6);

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), giftEvaluator, new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(); // the order still places; the gift is simply dropped
        result.Value.Order.Lines.Should().NotContain(l => l.IsGift);
        giftEvaluator.ReserveCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_SkipGiftLine_When_ProductPriceLookupHasNoName()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var giftProductId = Guid.NewGuid();
        var promotionId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m); // no entry for giftProductId
        var giftEvaluator = new FakeGiftPromotionEvaluator().WithGrant(promotionId, giftProductId, 1);
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 1).WithAvailable(giftProductId, 5);

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), giftEvaluator, new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Order.Lines.Should().NotContain(l => l.IsGift);
    }

    [Fact]
    public async Task Handle_Should_ReleaseGifts_When_SaveFailsAfterReserve()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var giftProductId = Guid.NewGuid();
        var promotionId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m).Add(giftProductId, "Nón", 50000m);
        var giftEvaluator = new FakeGiftPromotionEvaluator().WithGrant(promotionId, giftProductId, 1);
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 1).WithAvailable(giftProductId, 5);

        var handler = new PlaceOrderCommandHandler(
            orders, new FakeFailingUnitOfWork(), cart, addresses, prices, new FakePromotionEvaluator(), giftEvaluator, new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null), CancellationToken.None));

        giftEvaluator.ReleasedOrderIds.Should().HaveCount(1);
        giftEvaluator.ReserveCalls.Should().ContainSingle(c => c.OrderId == giftEvaluator.ReleasedOrderIds[0]);
    }

    [Fact]
    public async Task Handle_Should_AddOnlyInStockGiftLine_When_SamePromotionGrantsOneUnavailableAndOneAvailableProduct()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var giftProductX = Guid.NewGuid(); // out of stock, must be dropped
        var giftProductY = Guid.NewGuid(); // in stock, must be kept
        var promotionId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup()
            .Add(productId, "Widget", 100000m)
            .Add(giftProductX, "Gift X", 20000m)
            .Add(giftProductY, "Gift Y", 30000m);
        var giftEvaluator = new FakeGiftPromotionEvaluator()
            .WithGrant(promotionId, giftProductX, 1)
            .WithGrant(promotionId, giftProductY, 1);
        var stock = new FakeStockAvailabilityReader()
            .WithAvailable(productId, 1)
            .WithAvailable(giftProductX, 0)
            .WithAvailable(giftProductY, 5);

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), giftEvaluator, new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var giftLine = result.Value.Order.Lines.Should().ContainSingle(l => l.IsGift).Subject;
        giftLine.ProductId.Should().Be(giftProductY);
        giftEvaluator.ReserveCalls.Should().ContainSingle(c => c.PromotionIds.SequenceEqual(new[] { promotionId }));
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_IdempotencyKeyFormatInvalid()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();

        var handler = new PlaceOrderCommandHandler(
            orders, db, new FakeCartReader(), new FakeUserAddressReader(), new FakeProductPriceLookup(), new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), new FakeStockAvailabilityReader(), DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());

        var tooLong = new string('a', 101);
        var result = await handler.Handle(new PlaceOrderCommand(userId, Guid.NewGuid(), "COD", null, null, tooLong), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.InvalidIdempotencyKey");
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_IdempotencyKeyHasDisallowedCharacters()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();

        var handler = new PlaceOrderCommandHandler(
            orders, db, new FakeCartReader(), new FakeUserAddressReader(), new FakeProductPriceLookup(), new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), new FakeStockAvailabilityReader(), DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());

        var result = await handler.Handle(new PlaceOrderCommand(userId, Guid.NewGuid(), "COD", null, null, "has space"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.InvalidIdempotencyKey");
    }

    [Fact]
    public async Task Handle_Should_ReplayExistingOrder_When_SameIdempotencyKeyAndSameRequest()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        const string key = "key-123";

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 1);
        var idempotencyKeys = new FakeOrderIdempotencyKeyRepository();

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), idempotencyKeys, new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());

        var first = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", "note", null, key), CancellationToken.None);
        first.IsSuccess.Should().BeTrue();
        first.Value.Replayed.Should().BeFalse();

        var second = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", "note", null, key), CancellationToken.None);

        second.IsSuccess.Should().BeTrue();
        second.Value.Replayed.Should().BeTrue();
        second.Value.Order.Id.Should().Be(first.Value.Order.Id);
        (await db.Orders.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_Should_PersistIdempotencyKeyRow_When_OrderPlacedWithKey()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        const string key = "key-456";

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 1);
        var idempotencyKeys = new FakeOrderIdempotencyKeyRepository();

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), idempotencyKeys, new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", "note", null, key), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var stored = await idempotencyKeys.GetAsync(userId, key);
        stored.Should().NotBeNull();
        stored!.OrderId.Should().Be(result.Value.Order.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_When_SameIdempotencyKeyButDifferentRequest()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        const string key = "key-789";

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 1);
        var idempotencyKeys = new FakeOrderIdempotencyKeyRepository();

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), idempotencyKeys, new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());

        var first = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", "note one", null, key), CancellationToken.None);
        first.IsSuccess.Should().BeTrue();

        var second = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", "a different note", null, key), CancellationToken.None);

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("Orders.IdempotencyKeyReused");
        (await db.Orders.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_Should_PlaceOrder_When_NoIdempotencyKeyProvided()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 2);
        var idempotencyKeys = new FakeOrderIdempotencyKeyRepository();

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), idempotencyKeys, new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());

        var first = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null), CancellationToken.None);
        var second = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        second.Value.Order.Id.Should().NotBe(first.Value.Order.Id); // absent key -> no replay, two orders
        (await db.Orders.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Handle_Should_NotAcquireLock_When_NoIdempotencyKeyProvided()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 1);
        var idempotencyLock = new FakeIdempotencyLock();

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), idempotencyLock, TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        idempotencyLock.AcquireCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_AcquireLock_When_IdempotencyKeyProvided()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        const string key = "key-lock";

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 1);
        var idempotencyLock = new FakeIdempotencyLock();

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), idempotencyLock, TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", "note", null, key), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        idempotencyLock.AcquireCalls.Should().ContainSingle(c => c.UserId == userId && c.Key == key);
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_When_IdempotencyLockTimesOut()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        const string key = "key-busy";

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 1);
        var idempotencyLock = new FakeIdempotencyLock().WithNextAcquireFailing();

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), idempotencyLock, TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", "note", null, key), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.IdempotencyKeyInProgress");
        (await db.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_UseFlashPrice_When_FlashSaleQuotesTheLine()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 2, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 2);
        var flashSaleItemId = Guid.NewGuid();
        var flashSales = new FakeFlashSaleEvaluator { NextQuotes = new[] { new FlashSaleQuote(productId, flashSaleItemId, 60000m) } };

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), flashSales, stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Order.Lines.Should().ContainSingle(l => l.ProductId == productId && l.UnitPrice == 60000m);
        result.Value.Order.Subtotal.Should().Be(120000m);
        flashSales.ReserveCalls.Should().ContainSingle(c => c.OrderId == result.Value.Order.Id && c.UserId == userId);
    }

    [Fact]
    public async Task Handle_Should_UseActualReservedFlashPrice_When_ItDiffersFromAnEarlierPreviewQuote()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 1);
        var cheapItemId = Guid.NewGuid();
        var pricierItemId = Guid.NewGuid();
        // Simulates the old race this fix closes: a preview quote picked the cheaper campaign, but by the
        // time the atomic reservation ran its quota was gone, so a pricier campaign was actually reserved.
        // The order line must be priced at the ACTUAL reservation, never the stale preview.
        var flashSales = new FakeFlashSaleEvaluator
        {
            NextQuotes = new[] { new FlashSaleQuote(productId, cheapItemId, 40000m) },
            NextReserveResult = new[] { new FlashSaleQuote(productId, pricierItemId, 70000m) }
        };

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), flashSales, stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Order.Lines.Should().ContainSingle(l => l.ProductId == productId && l.UnitPrice == 70000m);
        result.Value.Order.Subtotal.Should().Be(70000m);
    }

    [Fact]
    public async Task Handle_Should_FallBackToRegularPrice_When_FlashSaleQuotaExhausted()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 1);
        var flashSales = new FakeFlashSaleEvaluator(); // NextQuotes empty -- no active flash sale for this product

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), flashSales, stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Order.Lines.Should().ContainSingle(l => l.ProductId == productId && l.UnitPrice == 100000m);
        // Reserve is now the single call (no separate preview Quote) -- it is still made, it just comes back empty.
        flashSales.ReserveCalls.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_Should_ReleaseFlashSaleReservation_When_SaveFailsAfterReserve()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 1);
        var flashSaleItemId = Guid.NewGuid();
        var flashSales = new FakeFlashSaleEvaluator { NextQuotes = new[] { new FlashSaleQuote(productId, flashSaleItemId, 60000m) } };

        var handler = new PlaceOrderCommandHandler(
            orders, new FakeFailingUnitOfWork(), cart, addresses, prices, new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), flashSales, stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null), CancellationToken.None));

        flashSales.ReleasedOrderIds.Should().HaveCount(1);
        flashSales.ReserveCalls.Should().ContainSingle(c => c.OrderId == flashSales.ReleasedOrderIds[0]);
    }

    [Fact]
    public async Task Handle_Should_PriceAndStockEachVariantSeparately_When_CartHasTwoVariantsOfSameProduct()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var variantRed = Guid.NewGuid();
        var variantBlue = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND",
            new CartSnapshotLine(productId, "Áo thun", 2, 100000m, variantRed),
            new CartSnapshotLine(productId, "Áo thun", 1, 100000m, variantBlue));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup()
            .AddVariant(productId, variantRed, "Áo thun", 100000m, "Đỏ")
            .AddVariant(productId, variantBlue, "Áo thun", 120000m, "Xanh");
        var stock = new FakeStockAvailabilityReader().WithAvailable(variantRed, 2).WithAvailable(variantBlue, 1);

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Order.Lines.Should().HaveCount(2);
        result.Value.Order.Lines.Should().ContainSingle(l => l.VariantId == variantRed && l.VariantLabel == "Đỏ" && l.UnitPrice == 100000m && l.Quantity == 2);
        result.Value.Order.Lines.Should().ContainSingle(l => l.VariantId == variantBlue && l.VariantLabel == "Xanh" && l.UnitPrice == 120000m && l.Quantity == 1);
        result.Value.Order.Subtotal.Should().Be(320000m);
    }

    [Fact]
    public async Task Handle_Should_ReturnInsufficientStockForThatVariantOnly_When_OneOfTwoVariantsIsShort()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var variantRed = Guid.NewGuid();
        var variantBlue = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND",
            new CartSnapshotLine(productId, "Áo thun", 2, 100000m, variantRed),
            new CartSnapshotLine(productId, "Áo thun", 1, 100000m, variantBlue));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup()
            .AddVariant(productId, variantRed, "Áo thun", 100000m, "Đỏ")
            .AddVariant(productId, variantBlue, "Áo thun", 120000m, "Xanh");
        var stock = new FakeStockAvailabilityReader().WithAvailable(variantRed, 2).WithAvailable(variantBlue, 0); // blue short

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.InsufficientStock");
        result.Error.Message.Should().Contain("Áo thun – Xanh");
        result.Error.Message.Should().NotContain("Đỏ");
        (await db.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_ApplyFlashPriceToMatchingVariantOnly_When_OnlyOneVariantIsFlashSaleQuoted()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var variantRed = Guid.NewGuid();
        var variantBlue = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND",
            new CartSnapshotLine(productId, "Áo thun", 1, 100000m, variantRed),
            new CartSnapshotLine(productId, "Áo thun", 1, 100000m, variantBlue));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup()
            .AddVariant(productId, variantRed, "Áo thun", 100000m, "Đỏ")
            .AddVariant(productId, variantBlue, "Áo thun", 100000m, "Xanh");
        var stock = new FakeStockAvailabilityReader().WithAvailable(variantRed, 1).WithAvailable(variantBlue, 1);
        var flashSaleItemId = Guid.NewGuid();
        var flashSales = new FakeFlashSaleEvaluator { NextQuotes = new[] { new FlashSaleQuote(productId, flashSaleItemId, 60000m, variantRed) } };

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), flashSales, stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Order.Lines.Should().ContainSingle(l => l.VariantId == variantRed && l.UnitPrice == 60000m);
        result.Value.Order.Lines.Should().ContainSingle(l => l.VariantId == variantBlue && l.UnitPrice == 100000m);
    }

    [Fact]
    public async Task Handle_Should_AddGiftLineWithGrantedVariant_When_GiftPromotionGrantsASpecificVariant()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var giftProductId = Guid.NewGuid();
        var giftVariantId = Guid.NewGuid();
        var promotionId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 2, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup()
            .Add(productId, "Widget", 100000m)
            .AddVariant(giftProductId, giftVariantId, "Nón lưỡi trai", 50000m, "Đen");
        var giftEvaluator = new FakeGiftPromotionEvaluator().WithGrant(promotionId, giftProductId, 1, variantId: giftVariantId);
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 2).WithAvailable(giftVariantId, 5);

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), giftEvaluator, new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var giftLine = result.Value.Order.Lines.Should().ContainSingle(l => l.IsGift).Subject;
        giftLine.VariantId.Should().Be(giftVariantId);
        giftLine.VariantLabel.Should().Be("Đen");
    }

    [Fact]
    public async Task Handle_Should_ReleaseFlashSaleReservation_When_CouponInvalid_AndFlashSaleLineActive()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 1);
        var promotions = new FakePromotionEvaluator().WithError("EXPIRED", "Mã giảm giá đã hết hạn");
        var flashSaleItemId = Guid.NewGuid();
        var flashSales = new FakeFlashSaleEvaluator { NextQuotes = new[] { new FlashSaleQuote(productId, flashSaleItemId, 60000m) } };

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, promotions, new FakeGiftPromotionEvaluator(), flashSales, stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null, "EXPIRED"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.InvalidCoupon");
        flashSales.ReleasedOrderIds.Should().HaveCount(1);
        flashSales.ReserveCalls.Should().ContainSingle(c => c.OrderId == flashSales.ReleasedOrderIds[0]);
        (await db.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_ReleaseFlashSaleAndCoupon_When_GiftReserveThrows()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var giftProductId = Guid.NewGuid();
        var promotionId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m).Add(giftProductId, "Nón", 50000m);
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 1).WithAvailable(giftProductId, 5);
        var promotions = new FakePromotionEvaluator().WithQuote("SALE10", 10000m);
        var giftEvaluator = new FakeGiftPromotionEvaluator().WithGrant(promotionId, giftProductId, 1).WithReserveGiftsException();
        var flashSaleItemId = Guid.NewGuid();
        var flashSales = new FakeFlashSaleEvaluator { NextQuotes = new[] { new FlashSaleQuote(productId, flashSaleItemId, 60000m) } };

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, promotions, giftEvaluator, flashSales, stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), new FakeLoyaltyPointsService());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null, "SALE10"), CancellationToken.None));

        flashSales.ReleasedOrderIds.Should().HaveCount(1);
        promotions.ReleasedOrderIds.Should().HaveCount(1);
        (await db.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_ReservePointsAgainstSubtotalExcludingItemsCoupon_When_UsePointsAndCouponTargetsItems()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 2, 100000m)); // subtotal 200000
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);
        var promotions = new FakePromotionEvaluator().WithQuote("SALE10", 20000m, discountTarget: "Items");
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 2);
        var loyalty = new FakeLoyaltyPointsService().WithRedemption(points: 1000, discountAmount: 1000m);

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, promotions, new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), loyalty);
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null, "SALE10", UsePoints: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        loyalty.ReserveCalls.Should().ContainSingle(c => c.UserId == userId && c.OrderId == result.Value.Order.Id && c.EligibleAmount == 180000m && c.OrderCode == result.Value.Order.Code); // 200000 - 20000 Items discount
        result.Value.Order.Subtotal.Should().Be(200000m);
        result.Value.Order.DiscountAmount.Should().Be(20000m);
        result.Value.Order.Total.Should().Be(209000m); // 200000 - 20000 - 1000 + 30000
    }

    [Fact]
    public async Task Handle_Should_ReservePointsAgainstFullSubtotal_When_UsePointsAndCouponTargetsShipping()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 199000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 199000m);
        var promotions = new FakePromotionEvaluator().WithQuote("FREESHIP", 30000m, discountTarget: "Shipping");
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 1);
        var loyalty = new FakeLoyaltyPointsService().WithRedemption(points: 500, discountAmount: 500m);

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, promotions, new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), loyalty);
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null, "FREESHIP", UsePoints: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        loyalty.ReserveCalls.Should().ContainSingle(c => c.EligibleAmount == 199000m); // shipping-target coupon doesn't shrink the points-eligible amount
    }

    [Fact]
    public async Task Handle_Should_NotCallLoyalty_When_UsePointsIsFalse()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 1);
        var loyalty = new FakeLoyaltyPointsService().WithRedemption(points: 1000, discountAmount: 1000m);

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), loyalty);
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        loyalty.ReserveCalls.Should().BeEmpty();
        result.Value.Order.PointsUsed.Should().Be(0);
        result.Value.Order.Total.Should().Be(130000m);
    }

    [Fact]
    public async Task Handle_Should_PlaceOrderWithoutPoints_When_UsePointsIsTrueButNothingUsable()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 1);
        var loyalty = new FakeLoyaltyPointsService(); // (null, null) -- zero balance, nothing usable

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), loyalty);
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null, UsePoints: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        loyalty.ReserveCalls.Should().ContainSingle();
        result.Value.Order.PointsUsed.Should().Be(0);
        result.Value.Order.Total.Should().Be(130000m);
    }

    [Fact]
    public async Task Handle_Should_ReleaseFlashAndCouponWithoutPersisting_When_PointsReserveFails()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 1);
        var promotions = new FakePromotionEvaluator().WithQuote("SALE10", 10000m);
        var flashSaleItemId = Guid.NewGuid();
        var flashSales = new FakeFlashSaleEvaluator { NextQuotes = new[] { new FlashSaleQuote(productId, flashSaleItemId, 60000m) } };
        var loyalty = new FakeLoyaltyPointsService().WithError("LOCKED", "Tài khoản bị khóa điểm");

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, promotions, new FakeGiftPromotionEvaluator(), flashSales, stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), loyalty);
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null, "SALE10", UsePoints: true), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.PointsUnavailable");
        result.Error.Message.Should().Be("Tài khoản bị khóa điểm");
        flashSales.ReleasedOrderIds.Should().HaveCount(1);
        promotions.ReleasedOrderIds.Should().HaveCount(1);
        (await db.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_ReleasePoints_When_SaveFailsAfterPointsReserve()
    {
        var (db, orders) = BuildScope();
        var userId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(productId, "Widget", 1, 100000m));
        var addresses = new FakeUserAddressReader().WithAddress(userId, addressId);
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 100000m);
        var stock = new FakeStockAvailabilityReader().WithAvailable(productId, 1);
        var loyalty = new FakeLoyaltyPointsService().WithRedemption(points: 1000, discountAmount: 1000m);

        var handler = new PlaceOrderCommandHandler(
            orders, new FakeFailingUnitOfWork(), cart, addresses, prices, new FakePromotionEvaluator(), new FakeGiftPromotionEvaluator(), new FakeFlashSaleEvaluator(), stock, DefaultShippingFeeCalculator(), new FakeOrderIdempotencyKeyRepository(), new FakeIdempotencyLock(), TimeProvider.System, DefaultCheckout(), loyalty);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new PlaceOrderCommand(userId, addressId, "COD", null, UsePoints: true), CancellationToken.None));

        loyalty.ReleasedOrderIds.Should().HaveCount(1);
        loyalty.ReserveCalls.Should().ContainSingle(c => c.OrderId == loyalty.ReleasedOrderIds[0]);
    }
}
