using System.Text.Json;
using ZIONShop.Contracts.Payments;
using ZIONShop.Payments.Application.Services;
using ZIONShop.Payments.Domain.Entities;
using ZIONShop.Payments.Domain.Enums;
using ZIONShop.Payments.Tests.TestSupport;

namespace ZIONShop.Payments.Tests.Services;

public class FakeWebhookProcessorTests
{
    private static (FakeWebhookProcessor Processor, FakePaymentsUnitOfWork Uow, Microsoft.EntityFrameworkCore.DbContext Db,
        ZIONShop.Payments.Infrastructure.Repositories.PaymentRepository Payments,
        ZIONShop.Payments.Infrastructure.Repositories.PayableOrderRepository PayableOrders,
        ZIONShop.Payments.Infrastructure.Repositories.ProcessedWebhookEventRepository ProcessedEvents,
        ZIONShop.Payments.Infrastructure.Gateway.FakePaymentGateway Gateway) Build()
    {
        var (db, payments, payableOrders, processedEvents) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var gateway = PaymentsTestFactory.BuildGateway();
        var processor = new FakeWebhookProcessor(payments, payableOrders, processedEvents, uow, gateway, TimeProvider.System);
        return (processor, uow, db, payments, payableOrders, processedEvents, gateway);
    }

    private static string Serialize(FakeWebhookPayload payload) => JsonSerializer.Serialize(payload, FakeWebhookJson.Options);

    [Fact]
    public async Task ProcessAsync_Should_ReturnUnauthorized_When_SignatureIsInvalid()
    {
        var ctx = Build();
        var rawBody = Serialize(new FakeWebhookPayload(Guid.NewGuid(), "fake_x", "succeeded", null));

        var result = await ctx.Processor.ProcessAsync(rawBody, "bad-signature", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payments.InvalidSignature");
    }

    [Fact]
    public async Task ProcessAsync_Should_MarkSucceeded_And_ProjectionPaid_And_QueueOutboxEvent_When_ResultSucceeded()
    {
        var ctx = Build();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var projection = PayableOrder.Create(orderId, userId, 200000m, "VND", DateTime.UtcNow);
        projection.MarkPayable(DateTime.UtcNow);
        await ctx.PayableOrders.AddAsync(projection);

        var payment = Payment.Create(orderId, userId, 200000m, "VND", "Fake", "key-1", DateTime.UtcNow);
        payment.AssignIntent("fake_" + Guid.NewGuid().ToString("N")[..24]);
        await ctx.Payments.AddAsync(payment);
        await ctx.Db.SaveChangesAsync();

        var payload = new FakeWebhookPayload(Guid.NewGuid(), payment.IntentId, "succeeded", null);
        var rawBody = Serialize(payload);
        var signature = ctx.Gateway.SignPayload(rawBody);

        var result = await ctx.Processor.ProcessAsync(rawBody, signature, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Succeeded");
        (await ctx.Payments.GetByIdAsync(payment.Id))!.Status.Should().Be(PaymentStatus.Succeeded);
        (await ctx.PayableOrders.GetByOrderIdAsync(orderId))!.State.Should().Be(PayableOrderState.Paid);
        ctx.Uow.OutboxEvents.OfType<PaymentCompletedIntegrationEvent>().Should().ContainSingle(e => e.OrderId == orderId && e.PaymentId == payment.Id);
    }

    [Fact]
    public async Task ProcessAsync_Should_MarkFailed_And_QueueOutboxEvent_When_ResultFailed()
    {
        var ctx = Build();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var projection = PayableOrder.Create(orderId, userId, 200000m, "VND", DateTime.UtcNow);
        projection.MarkPayable(DateTime.UtcNow);
        await ctx.PayableOrders.AddAsync(projection);

        var payment = Payment.Create(orderId, userId, 200000m, "VND", "Fake", "key-1", DateTime.UtcNow);
        payment.AssignIntent("fake_" + Guid.NewGuid().ToString("N")[..24]);
        await ctx.Payments.AddAsync(payment);
        await ctx.Db.SaveChangesAsync();

        var payload = new FakeWebhookPayload(Guid.NewGuid(), payment.IntentId, "failed", "Thẻ bị từ chối");
        var rawBody = Serialize(payload);
        var signature = ctx.Gateway.SignPayload(rawBody);

        var result = await ctx.Processor.ProcessAsync(rawBody, signature, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Failed");
        (await ctx.Payments.GetByIdAsync(payment.Id))!.FailureReason.Should().Be("Thẻ bị từ chối");
        ctx.Uow.OutboxEvents.OfType<PaymentFailedIntegrationEvent>().Should().ContainSingle(e => e.OrderId == orderId && e.Reason == "Thẻ bị từ chối");

        // Projection stays Payable here: cancellation to the final Cancelled state only happens via the
        // OrderCancelledIntegrationEvent roundtrip once Orders reacts to PaymentFailedIntegrationEvent.
        (await ctx.PayableOrders.GetByOrderIdAsync(orderId))!.State.Should().Be(PayableOrderState.Payable);
    }

    [Fact]
    public async Task ProcessAsync_Should_NotApplyEffectsTwice_When_SameEventIdReplayed()
    {
        var ctx = Build();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var projection = PayableOrder.Create(orderId, userId, 200000m, "VND", DateTime.UtcNow);
        projection.MarkPayable(DateTime.UtcNow);
        await ctx.PayableOrders.AddAsync(projection);

        var payment = Payment.Create(orderId, userId, 200000m, "VND", "Fake", "key-1", DateTime.UtcNow);
        payment.AssignIntent("fake_" + Guid.NewGuid().ToString("N")[..24]);
        await ctx.Payments.AddAsync(payment);
        await ctx.Db.SaveChangesAsync();

        var eventId = Guid.NewGuid();
        var payload = new FakeWebhookPayload(eventId, payment.IntentId, "succeeded", null);
        var rawBody = Serialize(payload);
        var signature = ctx.Gateway.SignPayload(rawBody);

        var first = await ctx.Processor.ProcessAsync(rawBody, signature, CancellationToken.None);
        var replayed = await ctx.Processor.ProcessAsync(rawBody, signature, CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        replayed.IsSuccess.Should().BeTrue();
        ctx.Uow.OutboxEvents.OfType<PaymentCompletedIntegrationEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task ProcessAsync_Should_Reject_When_ProjectionIsCancelled()
    {
        var ctx = Build();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var projection = PayableOrder.Create(orderId, userId, 200000m, "VND", DateTime.UtcNow);
        projection.MarkCancelled(DateTime.UtcNow);
        await ctx.PayableOrders.AddAsync(projection);

        var payment = Payment.Create(orderId, userId, 200000m, "VND", "Fake", "key-1", DateTime.UtcNow);
        payment.AssignIntent("fake_" + Guid.NewGuid().ToString("N")[..24]);
        await ctx.Payments.AddAsync(payment);
        await ctx.Db.SaveChangesAsync();

        var payload = new FakeWebhookPayload(Guid.NewGuid(), payment.IntentId, "succeeded", null);
        var rawBody = Serialize(payload);
        var signature = ctx.Gateway.SignPayload(rawBody);

        var result = await ctx.Processor.ProcessAsync(rawBody, signature, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payments.PaymentNotPending");
        ctx.Uow.OutboxEvents.Should().BeEmpty();
        (await ctx.Payments.GetByIdAsync(payment.Id))!.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public async Task ProcessAsync_Should_Reject_When_PaymentAlreadyCancelled()
    {
        var ctx = Build();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var projection = PayableOrder.Create(orderId, userId, 200000m, "VND", DateTime.UtcNow);
        projection.MarkPayable(DateTime.UtcNow);
        await ctx.PayableOrders.AddAsync(projection);

        var payment = Payment.Create(orderId, userId, 200000m, "VND", "Fake", "key-1", DateTime.UtcNow);
        payment.AssignIntent("fake_" + Guid.NewGuid().ToString("N")[..24]);
        payment.Cancel();
        await ctx.Payments.AddAsync(payment);
        await ctx.Db.SaveChangesAsync();

        var payload = new FakeWebhookPayload(Guid.NewGuid(), payment.IntentId, "succeeded", null);
        var rawBody = Serialize(payload);
        var signature = ctx.Gateway.SignPayload(rawBody);

        var result = await ctx.Processor.ProcessAsync(rawBody, signature, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payments.PaymentNotPending");
        ctx.Uow.OutboxEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_Should_ReturnNotFound_When_IntentIdUnknown()
    {
        var ctx = Build();
        var payload = new FakeWebhookPayload(Guid.NewGuid(), "fake_unknown", "succeeded", null);
        var rawBody = Serialize(payload);
        var signature = ctx.Gateway.SignPayload(rawBody);

        var result = await ctx.Processor.ProcessAsync(rawBody, signature, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payments.PaymentNotFound");
    }
}
