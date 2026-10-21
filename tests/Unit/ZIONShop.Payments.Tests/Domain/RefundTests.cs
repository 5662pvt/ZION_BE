using ZIONShop.Payments.Domain.Entities;
using ZIONShop.Payments.Domain.Enums;

namespace ZIONShop.Payments.Tests.Domain;

public class RefundTests
{
    private static Refund CreatePending(DateTime? now = null, Guid? paymentId = null) =>
        Refund.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), paymentId, 100000m, "VND", "Online", now ?? DateTime.UtcNow);

    [Fact]
    public void Create_Should_SetPendingStatus()
    {
        var refund = CreatePending();

        refund.Status.Should().Be(RefundStatus.Pending);
        refund.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void MarkSucceeded_Should_SetSucceededStatus_And_ProviderRefundId_When_Pending()
    {
        var refund = CreatePending();
        var now = DateTime.UtcNow;

        var result = refund.MarkSucceeded("fakerefund_abc123", now);

        result.IsSuccess.Should().BeTrue();
        refund.Status.Should().Be(RefundStatus.Succeeded);
        refund.ProviderRefundId.Should().Be("fakerefund_abc123");
        refund.CompletedAt.Should().Be(now);
    }

    [Fact]
    public void MarkFailed_Should_SetFailedStatus_And_Reason_When_Pending()
    {
        var refund = CreatePending();

        var result = refund.MarkFailed("Succeeded payment not found", DateTime.UtcNow);

        result.IsSuccess.Should().BeTrue();
        refund.Status.Should().Be(RefundStatus.Failed);
        refund.FailureReason.Should().Be("Succeeded payment not found");
    }

    [Fact]
    public void MarkSucceeded_Should_Fail_When_AlreadySucceeded()
    {
        var refund = CreatePending();
        refund.MarkSucceeded("fakerefund_abc123", DateTime.UtcNow);

        var result = refund.MarkSucceeded("fakerefund_other", DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payments.RefundNotPending");
    }

    [Fact]
    public void MarkFailed_Should_Fail_When_AlreadyFailed()
    {
        var refund = CreatePending();
        refund.MarkFailed("first reason", DateTime.UtcNow);

        var result = refund.MarkFailed("second reason", DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payments.RefundNotPending");
    }
}
