using ZIONShop.Payments.Application.Features.SimulateFakePayment;
using ZIONShop.Payments.Application.Services;
using ZIONShop.Payments.Domain.Entities;
using ZIONShop.Payments.Tests.TestSupport;

namespace ZIONShop.Payments.Tests.Features;

public class SimulateFakePaymentCommandHandlerTests
{
    private static (SimulateFakePaymentCommandHandler Handler, FakePaymentsUnitOfWork Uow, Payment Payment) Build(string environmentName = "Development")
    {
        var (db, payments, payableOrders, processedEvents) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var gateway = PaymentsTestFactory.BuildGateway();
        var processor = new FakeWebhookProcessor(payments, payableOrders, processedEvents, uow, gateway, TimeProvider.System);
        var environment = new FakeHostEnvironment(environmentName);

        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projection = PayableOrder.Create(orderId, userId, 200000m, "VND", DateTime.UtcNow);
        projection.MarkPayable(DateTime.UtcNow);
        payableOrders.AddAsync(projection).GetAwaiter().GetResult();

        var payment = Payment.Create(orderId, userId, 200000m, "VND", "Fake", "key-1", DateTime.UtcNow);
        payment.AssignIntent("fake_" + Guid.NewGuid().ToString("N")[..24]);
        payments.AddAsync(payment).GetAwaiter().GetResult();
        db.SaveChangesAsync().GetAwaiter().GetResult();

        var handler = new SimulateFakePaymentCommandHandler(payments, gateway, processor, environment);
        return (handler, uow, payment);
    }

    [Fact]
    public async Task Handle_Should_MarkSucceeded_When_ResultIsSuccess()
    {
        var (handler, _, payment) = Build();

        var result = await handler.Handle(new SimulateFakePaymentCommand(payment.IntentId, payment.UserId, "success"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Succeeded");
    }

    [Fact]
    public async Task Handle_Should_MarkFailed_When_ResultIsFailure()
    {
        var (handler, _, payment) = Build();

        var result = await handler.Handle(new SimulateFakePaymentCommand(payment.IntentId, payment.UserId, "failure"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Failed");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_NotDevelopmentEnvironment()
    {
        var (handler, _, payment) = Build(environmentName: "Production");

        var result = await handler.Handle(new SimulateFakePaymentCommand(payment.IntentId, payment.UserId, "success"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payments.SimulationNotAvailable");
    }

    [Fact]
    public async Task Handle_Should_ReturnValidationError_When_ResultIsNotRecognized()
    {
        var (handler, _, payment) = Build();

        var result = await handler.Handle(new SimulateFakePaymentCommand(payment.IntentId, payment.UserId, "bogus"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payments.InvalidSimulationResult");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_RequestedByNonOwner()
    {
        var (handler, _, payment) = Build();

        var result = await handler.Handle(new SimulateFakePaymentCommand(payment.IntentId, Guid.NewGuid(), "success"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payments.PaymentNotFound");
    }
}
