using ZIONShop.Payments.Application.Features.GetPaymentIntent;
using ZIONShop.Payments.Domain.Entities;
using ZIONShop.Payments.Tests.TestSupport;

namespace ZIONShop.Payments.Tests.Features;

public class GetPaymentIntentQueryHandlerTests
{
    [Fact]
    public async Task Handle_Should_ReturnDto_When_OwnerQueries()
    {
        var (db, payments, _, _) = PaymentsTestFactory.BuildScope();
        var gateway = PaymentsTestFactory.BuildGateway();
        var userId = Guid.NewGuid();
        var payment = Payment.Create(Guid.NewGuid(), userId, 100000m, "VND", "Fake", "key-1", DateTime.UtcNow);
        payment.AssignIntent("fake_abc123");
        await payments.AddAsync(payment);
        await db.SaveChangesAsync();

        var handler = new GetPaymentIntentQueryHandler(payments, gateway);
        var result = await handler.Handle(new GetPaymentIntentQuery("fake_abc123", userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IntentId.Should().Be("fake_abc123");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_RequestedByNonOwner()
    {
        var (db, payments, _, _) = PaymentsTestFactory.BuildScope();
        var gateway = PaymentsTestFactory.BuildGateway();
        var payment = Payment.Create(Guid.NewGuid(), Guid.NewGuid(), 100000m, "VND", "Fake", "key-1", DateTime.UtcNow);
        payment.AssignIntent("fake_abc123");
        await payments.AddAsync(payment);
        await db.SaveChangesAsync();

        var handler = new GetPaymentIntentQueryHandler(payments, gateway);
        var result = await handler.Handle(new GetPaymentIntentQuery("fake_abc123", Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payments.PaymentNotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_IntentIdUnknown()
    {
        var (_, payments, _, _) = PaymentsTestFactory.BuildScope();
        var gateway = PaymentsTestFactory.BuildGateway();

        var handler = new GetPaymentIntentQueryHandler(payments, gateway);
        var result = await handler.Handle(new GetPaymentIntentQuery("fake_unknown", Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payments.PaymentNotFound");
    }
}
