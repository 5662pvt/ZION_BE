using ZIONShop.Payments.Application.Features.CreatePaymentIntent;
using ZIONShop.Payments.Domain.Entities;
using ZIONShop.Payments.Tests.TestSupport;

namespace ZIONShop.Payments.Tests.Features;

public class CreatePaymentIntentCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_CreateIntent_When_OrderIsPayable()
    {
        var (db, payments, payableOrders, _) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var gateway = PaymentsTestFactory.BuildGateway();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var projection = PayableOrder.Create(orderId, userId, 250000m, "VND", DateTime.UtcNow);
        projection.MarkPayable(DateTime.UtcNow);
        await payableOrders.AddAsync(projection);
        await db.SaveChangesAsync();

        var handler = new CreatePaymentIntentCommandHandler(payments, payableOrders, uow, gateway, TimeProvider.System);
        var result = await handler.Handle(new CreatePaymentIntentCommand(orderId, userId, "idem-key-1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.OrderId.Should().Be(orderId);
        result.Value.Amount.Should().Be(250000m);
        result.Value.Status.Should().Be("Pending");
        result.Value.IntentId.Should().StartWith("fake_");
        result.Value.RedirectUrl.Should().Contain(result.Value.IntentId);
    }

    [Fact]
    public async Task Handle_Should_ReturnExisting_When_SameIdempotencyKeyReused()
    {
        var (db, payments, payableOrders, _) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var gateway = PaymentsTestFactory.BuildGateway();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var projection = PayableOrder.Create(orderId, userId, 250000m, "VND", DateTime.UtcNow);
        projection.MarkPayable(DateTime.UtcNow);
        await payableOrders.AddAsync(projection);
        await db.SaveChangesAsync();

        var handler = new CreatePaymentIntentCommandHandler(payments, payableOrders, uow, gateway, TimeProvider.System);
        var first = await handler.Handle(new CreatePaymentIntentCommand(orderId, userId, "idem-key-1"), CancellationToken.None);
        var second = await handler.Handle(new CreatePaymentIntentCommand(orderId, userId, "idem-key-1"), CancellationToken.None);

        second.IsSuccess.Should().BeTrue();
        second.Value.IntentId.Should().Be(first.Value.IntentId);
        db.Payments.Count(p => p.OrderId == orderId).Should().Be(1);
    }

    [Fact]
    public async Task Handle_Should_ReturnExistingPendingIntent_When_CalledTwiceWithDifferentKeys()
    {
        var (db, payments, payableOrders, _) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var gateway = PaymentsTestFactory.BuildGateway();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var projection = PayableOrder.Create(orderId, userId, 250000m, "VND", DateTime.UtcNow);
        projection.MarkPayable(DateTime.UtcNow);
        await payableOrders.AddAsync(projection);
        await db.SaveChangesAsync();

        var handler = new CreatePaymentIntentCommandHandler(payments, payableOrders, uow, gateway, TimeProvider.System);
        var first = await handler.Handle(new CreatePaymentIntentCommand(orderId, userId, "idem-key-1"), CancellationToken.None);
        var second = await handler.Handle(new CreatePaymentIntentCommand(orderId, userId, "idem-key-2"), CancellationToken.None);

        second.Value.IntentId.Should().Be(first.Value.IntentId);
        db.Payments.Count(p => p.OrderId == orderId).Should().Be(1);
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_When_ProjectionIsStillAwaitingStock()
    {
        var (db, payments, payableOrders, _) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var gateway = PaymentsTestFactory.BuildGateway();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await payableOrders.AddAsync(PayableOrder.Create(orderId, userId, 250000m, "VND", DateTime.UtcNow));
        await db.SaveChangesAsync();

        var handler = new CreatePaymentIntentCommandHandler(payments, payableOrders, uow, gateway, TimeProvider.System);
        var result = await handler.Handle(new CreatePaymentIntentCommand(orderId, userId, "idem-key-1"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payments.OrderNotPayable");
    }

    [Fact]
    public async Task Handle_Should_ReturnBadRequest_When_OrderIsCancelled()
    {
        var (db, payments, payableOrders, _) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var gateway = PaymentsTestFactory.BuildGateway();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var projection = PayableOrder.Create(orderId, userId, 250000m, "VND", DateTime.UtcNow);
        projection.MarkCancelled(DateTime.UtcNow);
        await payableOrders.AddAsync(projection);
        await db.SaveChangesAsync();

        var handler = new CreatePaymentIntentCommandHandler(payments, payableOrders, uow, gateway, TimeProvider.System);
        var result = await handler.Handle(new CreatePaymentIntentCommand(orderId, userId, "idem-key-1"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payments.OrderCancelled");
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_When_OrderAlreadyPaid()
    {
        var (db, payments, payableOrders, _) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var gateway = PaymentsTestFactory.BuildGateway();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var projection = PayableOrder.Create(orderId, userId, 250000m, "VND", DateTime.UtcNow);
        projection.MarkPayable(DateTime.UtcNow);
        projection.MarkPaid(DateTime.UtcNow);
        await payableOrders.AddAsync(projection);
        await db.SaveChangesAsync();

        var handler = new CreatePaymentIntentCommandHandler(payments, payableOrders, uow, gateway, TimeProvider.System);
        var result = await handler.Handle(new CreatePaymentIntentCommand(orderId, userId, "idem-key-1"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payments.OrderAlreadyPaid");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_RequestingUserIsNotOwner()
    {
        var (db, payments, payableOrders, _) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var gateway = PaymentsTestFactory.BuildGateway();
        var orderId = Guid.NewGuid();

        var projection = PayableOrder.Create(orderId, Guid.NewGuid(), 250000m, "VND", DateTime.UtcNow);
        projection.MarkPayable(DateTime.UtcNow);
        await payableOrders.AddAsync(projection);
        await db.SaveChangesAsync();

        var handler = new CreatePaymentIntentCommandHandler(payments, payableOrders, uow, gateway, TimeProvider.System);
        var result = await handler.Handle(new CreatePaymentIntentCommand(orderId, Guid.NewGuid(), "idem-key-1"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payments.OrderNotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_When_ProjectionDoesNotExist()
    {
        var (db, payments, payableOrders, _) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var gateway = PaymentsTestFactory.BuildGateway();

        var handler = new CreatePaymentIntentCommandHandler(payments, payableOrders, uow, gateway, TimeProvider.System);
        var result = await handler.Handle(new CreatePaymentIntentCommand(Guid.NewGuid(), Guid.NewGuid(), "idem-key-1"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payments.OrderNotPayable");
    }
}
