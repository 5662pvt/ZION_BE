using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZIONShop.Contracts.Orders;
using ZIONShop.Notifications.Application.IntegrationEvents;
using ZIONShop.Notifications.Application.Options;
using ZIONShop.Notifications.Domain.Enums;
using ZIONShop.Notifications.Infrastructure.Persistence;
using ZIONShop.Notifications.Infrastructure.Repositories;
using ZIONShop.Notifications.Tests.Fakes;

namespace ZIONShop.Notifications.Tests.IntegrationEvents;

public class OrderConfirmedIntegrationEventHandlerTests
{
    private static (NotificationsDbContext db, NotificationLogRepository logs) BuildScope()
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new NotificationsDbContext(options);
        return (db, new NotificationLogRepository(db));
    }

    private static IOptions<NotificationsOptions> DefaultOptions() =>
        Options.Create(new NotificationsOptions { StorefrontBaseUrl = "http://localhost:3000" });

    private static OrderDetails BuildOrder(Guid orderId, Guid userId, decimal discount = 0, string? couponCode = null) => new(
        orderId, userId, "ZS0001", "Confirmed", "COD",
        200000m, discount, couponCode, null, 30000m, 230000m - discount, "VND",
        "Nguyen Van A", "0900000000", "123 Đường ABC, Cầu Giấy, Hà Nội", null, DateTime.UtcNow,
        new[] { new OrderDetailsLine("Widget", 2, 100000m, 200000m) });

    [Fact]
    public async Task HandleAsync_Should_LogSent_When_ContactAndOrderExist()
    {
        var (db, logs) = BuildScope();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var contacts = new FakeUserContactReader().With(userId, "customer@zionshop.local", "Nguyen Van A");
        var orders = new FakeOrderDetailsReader().With(BuildOrder(orderId, userId));
        var email = new FakeEmailSender();

        var handler = new OrderConfirmedIntegrationEventHandler(logs, db, contacts, orders, email, DefaultOptions());
        var @event = new OrderConfirmedIntegrationEvent(orderId, userId);

        await handler.HandleAsync(@event);

        email.Sent.Should().ContainSingle(s => s.To == "customer@zionshop.local");
        var log = logs.Query().Single();
        log.Status.Should().Be(NotificationStatus.Sent);
        log.Type.Should().Be(NotificationType.OrderConfirmed);
        log.EventId.Should().Be(@event.EventId);
    }

    [Fact]
    public async Task HandleAsync_Should_BeIdempotent_When_CalledTwiceForSameEvent()
    {
        var (db, logs) = BuildScope();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var contacts = new FakeUserContactReader().With(userId, "customer@zionshop.local");
        var orders = new FakeOrderDetailsReader().With(BuildOrder(orderId, userId));
        var email = new FakeEmailSender();

        var handler = new OrderConfirmedIntegrationEventHandler(logs, db, contacts, orders, email, DefaultOptions());
        var @event = new OrderConfirmedIntegrationEvent(orderId, userId);

        await handler.HandleAsync(@event);
        await handler.HandleAsync(@event); // redelivery must be a no-op

        email.Sent.Should().HaveCount(1);
        logs.Query().Count().Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_Should_LogSkipped_When_ContactNotFound()
    {
        var (db, logs) = BuildScope();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orders = new FakeOrderDetailsReader().With(BuildOrder(orderId, userId));
        var email = new FakeEmailSender();

        var handler = new OrderConfirmedIntegrationEventHandler(logs, db, new FakeUserContactReader(), orders, email, DefaultOptions());
        await handler.HandleAsync(new OrderConfirmedIntegrationEvent(orderId, userId));

        email.Sent.Should().BeEmpty();
        var log = logs.Query().Single();
        log.Status.Should().Be(NotificationStatus.Skipped);
    }

    [Fact]
    public async Task HandleAsync_Should_LogSkipped_When_ContactEmailIsMissing()
    {
        var (db, logs) = BuildScope();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var contacts = new FakeUserContactReader().With(userId, email: string.Empty);
        var orders = new FakeOrderDetailsReader().With(BuildOrder(orderId, userId));

        var handler = new OrderConfirmedIntegrationEventHandler(logs, db, contacts, orders, new FakeEmailSender(), DefaultOptions());
        await handler.HandleAsync(new OrderConfirmedIntegrationEvent(orderId, userId));

        var log = logs.Query().Single();
        log.Status.Should().Be(NotificationStatus.Skipped);
    }

    [Fact]
    public async Task HandleAsync_Should_LogFailed_When_EmailSendThrows()
    {
        var (db, logs) = BuildScope();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var contacts = new FakeUserContactReader().With(userId, "customer@zionshop.local");
        var orders = new FakeOrderDetailsReader().With(BuildOrder(orderId, userId));
        var email = new FakeEmailSender { ShouldThrow = true };

        var handler = new OrderConfirmedIntegrationEventHandler(logs, db, contacts, orders, email, DefaultOptions());
        await handler.HandleAsync(new OrderConfirmedIntegrationEvent(orderId, userId));

        var log = logs.Query().Single();
        log.Status.Should().Be(NotificationStatus.Failed);
        log.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task HandleAsync_Should_NoOp_When_OrderUnknown()
    {
        var (db, logs) = BuildScope();
        var userId = Guid.NewGuid();
        var contacts = new FakeUserContactReader().With(userId, "customer@zionshop.local");

        var handler = new OrderConfirmedIntegrationEventHandler(logs, db, contacts, new FakeOrderDetailsReader(), new FakeEmailSender(), DefaultOptions());
        await handler.HandleAsync(new OrderConfirmedIntegrationEvent(Guid.NewGuid(), userId));

        var log = logs.Query().Single();
        log.Status.Should().Be(NotificationStatus.Skipped);
    }
}
