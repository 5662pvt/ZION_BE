using ZIONShop.Payments.Domain.Entities;
using ZIONShop.Payments.Domain.Enums;

namespace ZIONShop.Payments.Tests.Domain;

public class PaymentTests
{
    private static Payment CreatePending(DateTime? now = null) =>
        Payment.Create(Guid.NewGuid(), Guid.NewGuid(), 100000m, "VND", "Fake", "idem-key-1", now ?? DateTime.UtcNow);

    [Fact]
    public void Create_Should_SetPendingStatus_And_FifteenMinuteExpiry()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var payment = CreatePending(now);

        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.ExpiresAt.Should().Be(now.AddMinutes(15));
    }

    [Fact]
    public void AssignIntent_Should_SetIntentId_When_NotAlreadyAssigned()
    {
        var payment = CreatePending();
        payment.AssignIntent("fake_abc123");
        payment.IntentId.Should().Be("fake_abc123");
    }

    [Fact]
    public void AssignIntent_Should_Throw_When_AlreadyAssigned()
    {
        var payment = CreatePending();
        payment.AssignIntent("fake_abc123");

        var act = () => payment.AssignIntent("fake_other");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Succeed_Should_SetSucceededStatus_And_CompletedAt_When_Pending()
    {
        var payment = CreatePending();
        var now = DateTime.UtcNow;

        var result = payment.Succeed(now);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Succeeded);
        payment.CompletedAt.Should().Be(now);
    }

    [Fact]
    public void Fail_Should_SetFailedStatus_And_Reason_When_Pending()
    {
        var payment = CreatePending();

        var result = payment.Fail("Thẻ bị từ chối");

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.FailureReason.Should().Be("Thẻ bị từ chối");
    }

    [Fact]
    public void Cancel_Should_SetCancelledStatus_When_Pending()
    {
        var payment = CreatePending();

        var result = payment.Cancel();

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Cancelled);
    }

    [Fact]
    public void Succeed_Should_Fail_When_AlreadySucceeded()
    {
        var payment = CreatePending();
        payment.Succeed(DateTime.UtcNow);

        var result = payment.Succeed(DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payments.PaymentNotPending");
    }

    [Fact]
    public void Fail_Should_Fail_When_AlreadyCancelled()
    {
        var payment = CreatePending();
        payment.Cancel();

        var result = payment.Fail("too late");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payments.PaymentNotPending");
    }

    [Fact]
    public void MarkRefunded_Should_SetRefundedStatus_And_CompletedAt_When_Succeeded()
    {
        var payment = CreatePending();
        payment.Succeed(DateTime.UtcNow);
        var now = DateTime.UtcNow;

        var result = payment.MarkRefunded(now);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.CompletedAt.Should().Be(now);
    }

    [Fact]
    public void MarkRefunded_Should_Fail_When_NotSucceeded()
    {
        var payment = CreatePending();

        var result = payment.MarkRefunded(DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payments.PaymentNotRefundable");
    }
}
