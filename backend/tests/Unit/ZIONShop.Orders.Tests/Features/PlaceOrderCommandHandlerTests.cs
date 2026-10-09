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

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, DefaultCheckout());
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
            orders, db, new FakeCartReader(), new FakeUserAddressReader(), new FakeProductPriceLookup(), DefaultCheckout());

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

        var handler = new PlaceOrderCommandHandler(orders, db, cart, new FakeUserAddressReader(), prices, DefaultCheckout());
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

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, DefaultCheckout());
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

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, DefaultCheckout());
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

        var handler = new PlaceOrderCommandHandler(orders, db, cart, addresses, prices, DefaultCheckout());
        var result = await handler.Handle(new PlaceOrderCommand(userId, addressId, "Online", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Lines.Single().UnitPrice.Should().Be(150000m);
        result.Value.Subtotal.Should().Be(150000m);
    }
}
