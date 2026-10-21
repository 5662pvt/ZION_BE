using Microsoft.Extensions.Logging.Abstractions;
using ZIONShop.Contracts.Orders;
using ZIONShop.Payments.Application.IntegrationEvents;
using ZIONShop.Payments.Domain.Entities;
using ZIONShop.Payments.Domain.Enums;
using ZIONShop.Payments.Infrastructure.Persistence;
using ZIONShop.Payments.Infrastructure.Repositories;
using ZIONShop.Payments.Tests.TestSupport;

namespace ZIONShop.Payments.Tests.IntegrationEvents;

public class OrderReturnReceivedIntegrationEventHandlerTests
{
    private static OrderReturnReceivedIntegrationEventHandler BuildHandler(
        PaymentsDbContext db,
        PaymentRepository payments,
        FakePaymentsUnitOfWork uow) =>
        new(new RefundRepository(db), payments, PaymentsTestFactory.BuildGateway(), uow, TimeProvider.System,
            NullLogger<OrderReturnReceivedIntegrationEventHandler>.Instance);

    [Fact]
    public async Task HandleAsync_Should_MarkPaymentRefunded_And_PublishOnlineEvent_When_OnlineWithSucceededPayment()
    {
        var (db, payments, _, _) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var returnRequestId = Guid.NewGuid();

        var payment = Payment.Create(orderId, userId, 150000m, "VND", "Fake", "key-1", DateTime.UtcNow);
        payment.AssignIntent("fake_" + Guid.NewGuid().ToString("N")[..24]);
        payment.Succeed(DateTime.UtcNow);
        await payments.AddAsync(payment);
        await db.SaveChangesAsync();

        var handler = BuildHandler(db, payments, uow);
        var @event = new OrderReturnReceivedIntegrationEvent(orderId, userId, returnRequestId, 150000m, "Online", Array.Empty<ReturnLine>());

        await handler.HandleAsync(@event);

        var refunds = new RefundRepository(db);
        var refund = await refunds.GetByReturnRequestIdAsync(returnRequestId);
        refund.Should().NotBeNull();
        refund!.Status.Should().Be(RefundStatus.Succeeded);
        refund.Method.Should().Be("Online");
        refund.PaymentId.Should().Be(payment.Id);
        refund.ProviderRefundId.Should().StartWith("fakerefund_");

        (await payments.GetByIdAsync(payment.Id))!.Status.Should().Be(PaymentStatus.Refunded);

        var published = uow.OutboxEvents.Should().ContainSingle().Subject.Should().BeOfType<OrderRefundedIntegrationEvent>().Subject;
        published.Method.Should().Be("Online");
    }

    [Fact]
    public async Task HandleAsync_Should_CreateSucceededRefund_And_PublishCodEvent_When_MethodIsCod()
    {
        var (db, payments, _, _) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var returnRequestId = Guid.NewGuid();

        var handler = BuildHandler(db, payments, uow);
        var @event = new OrderReturnReceivedIntegrationEvent(orderId, userId, returnRequestId, 80000m, "COD", Array.Empty<ReturnLine>());

        await handler.HandleAsync(@event);

        var refunds = new RefundRepository(db);
        var refund = await refunds.GetByReturnRequestIdAsync(returnRequestId);
        refund.Should().NotBeNull();
        refund!.Status.Should().Be(RefundStatus.Succeeded);
        refund.Method.Should().Be("COD");
        refund.PaymentId.Should().BeNull();
        refund.ProviderRefundId.Should().StartWith("cod_");

        var published = uow.OutboxEvents.Should().ContainSingle().Subject.Should().BeOfType<OrderRefundedIntegrationEvent>().Subject;
        published.Method.Should().Be("COD");
    }

    [Fact]
    public async Task HandleAsync_Should_BeIdempotent_When_CalledTwice()
    {
        var (db, payments, _, _) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var returnRequestId = Guid.NewGuid();

        var handler = BuildHandler(db, payments, uow);
        var @event = new OrderReturnReceivedIntegrationEvent(orderId, userId, returnRequestId, 80000m, "COD", Array.Empty<ReturnLine>());

        await handler.HandleAsync(@event);
        await handler.HandleAsync(@event);

        db.Refunds.Count(r => r.ReturnRequestId == returnRequestId).Should().Be(1);
        uow.OutboxEvents.Should().ContainSingle();
    }

    [Fact]
    public async Task HandleAsync_Should_CreateFailedRefund_And_NotPublishEvent_When_OnlineHasNoSucceededPayment()
    {
        var (db, payments, _, _) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var returnRequestId = Guid.NewGuid();

        var handler = BuildHandler(db, payments, uow);
        var @event = new OrderReturnReceivedIntegrationEvent(orderId, userId, returnRequestId, 150000m, "Online", Array.Empty<ReturnLine>());

        await handler.HandleAsync(@event);

        var refunds = new RefundRepository(db);
        var refund = await refunds.GetByReturnRequestIdAsync(returnRequestId);
        refund.Should().NotBeNull();
        refund!.Status.Should().Be(RefundStatus.Failed);
        refund.PaymentId.Should().BeNull();
        refund.FailureReason.Should().NotBeNullOrEmpty();

        uow.OutboxEvents.Should().BeEmpty();
    }
}
