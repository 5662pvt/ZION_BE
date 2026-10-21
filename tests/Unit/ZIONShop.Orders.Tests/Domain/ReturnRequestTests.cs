using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Enums;

namespace ZIONShop.Orders.Tests.Domain;

public class ReturnRequestTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static ReturnRequest CreateRequested() =>
        ReturnRequest.Create(Guid.NewGuid(), Guid.NewGuid(), ReturnReason.Defective, "Hỏng khi mở hộp", 150000m, Now).Value;

    [Fact]
    public void Create_Should_SetRequestedStatus_And_SnapshotRefundAmount()
    {
        var request = CreateRequested();

        request.Status.Should().Be(ReturnRequestStatus.Requested);
        request.RefundAmount.Should().Be(150000m);
        request.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void Create_Should_TrimAndCapNote_When_NoteExceedsMaxLength()
    {
        var longNote = "  " + new string('a', 1100) + "  ";
        var result = ReturnRequest.Create(Guid.NewGuid(), Guid.NewGuid(), ReturnReason.Other, longNote, 100000m, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Note.Should().HaveLength(1000);
    }

    [Fact]
    public void Approve_Should_Succeed_When_Requested()
    {
        var request = CreateRequested();
        var result = request.Approve(Now);

        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(ReturnRequestStatus.Approved);
        request.ApprovedAt.Should().Be(Now);
    }

    [Fact]
    public void Approve_Should_Fail_When_NotRequested()
    {
        var request = CreateRequested();
        request.Approve(Now);

        var result = request.Approve(Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.ReturnInvalidTransition");
    }

    [Fact]
    public void Reject_Should_Succeed_When_Requested_And_ReasonProvided()
    {
        var request = CreateRequested();
        var result = request.Reject("Quá thời hạn", Now);

        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(ReturnRequestStatus.Rejected);
        request.RejectReason.Should().Be("Quá thời hạn");
        request.RejectedAt.Should().Be(Now);
    }

    [Fact]
    public void Reject_Should_Fail_When_ReasonMissing()
    {
        var request = CreateRequested();
        var result = request.Reject("   ", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.ReturnRejectReasonRequired");
        request.Status.Should().Be(ReturnRequestStatus.Requested);
    }

    [Fact]
    public void Reject_Should_Fail_When_NotRequested()
    {
        var request = CreateRequested();
        request.Approve(Now);

        var result = request.Reject("Lý do", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.ReturnInvalidTransition");
    }

    [Fact]
    public void MarkShippedBack_Should_Succeed_When_Approved()
    {
        var request = CreateRequested();
        request.Approve(Now);

        var result = request.MarkShippedBack("VN123456", Now);

        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(ReturnRequestStatus.ShippedBack);
        request.ReturnTrackingCode.Should().Be("VN123456");
        request.ShippedBackAt.Should().Be(Now);
    }

    [Fact]
    public void MarkShippedBack_Should_Fail_When_NotApproved()
    {
        var request = CreateRequested();
        var result = request.MarkShippedBack(null, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.ReturnInvalidTransition");
    }

    [Fact]
    public void MarkReceived_Should_Succeed_When_ShippedBack()
    {
        var request = CreateRequested();
        request.Approve(Now);
        request.MarkShippedBack(null, Now);

        var result = request.MarkReceived(Now);

        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(ReturnRequestStatus.Received);
        request.ReceivedAt.Should().Be(Now);
    }

    [Fact]
    public void MarkReceived_Should_Succeed_When_ApprovedButNeverShippedBack()
    {
        var request = CreateRequested();
        request.Approve(Now);

        var result = request.MarkReceived(Now);

        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(ReturnRequestStatus.Received);
    }

    [Fact]
    public void MarkReceived_Should_Fail_When_Requested()
    {
        var request = CreateRequested();
        var result = request.MarkReceived(Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.ReturnInvalidTransition");
    }

    [Fact]
    public void MarkRefunded_Should_Succeed_When_Received()
    {
        var request = CreateRequested();
        request.Approve(Now);
        request.MarkReceived(Now);

        var result = request.MarkRefunded("Online", Now);

        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(ReturnRequestStatus.Refunded);
        request.RefundMethod.Should().Be("Online");
        request.RefundedAt.Should().Be(Now);
    }

    [Fact]
    public void MarkRefunded_Should_Fail_When_NotReceived()
    {
        var request = CreateRequested();
        var result = request.MarkRefunded("COD", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.ReturnInvalidTransition");
    }
}
