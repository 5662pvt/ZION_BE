using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.Exceptions;
using ZIONShop.SharedKernel.Entities;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Domain.Entities;

public class ReturnRequest : BaseEntity
{
    private const int NoteMaxLength = 1000;

    private ReturnRequest() { }

    public Guid OrderId { get; private set; }
    public Guid UserId { get; private set; }
    public ReturnReason Reason { get; private set; }
    public string? Note { get; private set; }
    public ReturnRequestStatus Status { get; private set; }
    public string? RejectReason { get; private set; }
    public string? ReturnTrackingCode { get; private set; }
    public decimal RefundAmount { get; private set; }
    public string? RefundMethod { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public DateTime? RejectedAt { get; private set; }
    public DateTime? ShippedBackAt { get; private set; }
    public DateTime? ReceivedAt { get; private set; }
    public DateTime? RefundedAt { get; private set; }

    public static Result<ReturnRequest> Create(Guid orderId, Guid userId, ReturnReason reason, string? note, decimal refundAmount, DateTime now)
    {
        var trimmedNote = note?.Trim();
        if (!string.IsNullOrEmpty(trimmedNote) && trimmedNote.Length > NoteMaxLength)
            trimmedNote = trimmedNote[..NoteMaxLength];

        return Result.Success(new ReturnRequest
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            UserId = userId,
            Reason = reason,
            Note = string.IsNullOrEmpty(trimmedNote) ? null : trimmedNote,
            RefundAmount = refundAmount,
            Status = ReturnRequestStatus.Requested,
            CreatedAt = now
        });
    }

    public Result Approve(DateTime now)
    {
        if (Status != ReturnRequestStatus.Requested) return Result.Failure(OrdersErrors.ReturnInvalidTransition);
        Status = ReturnRequestStatus.Approved;
        ApprovedAt = now;
        return Result.Success();
    }

    public Result Reject(string reason, DateTime now)
    {
        if (Status != ReturnRequestStatus.Requested) return Result.Failure(OrdersErrors.ReturnInvalidTransition);
        if (string.IsNullOrWhiteSpace(reason)) return Result.Failure(OrdersErrors.ReturnRejectReasonRequired);

        Status = ReturnRequestStatus.Rejected;
        RejectReason = reason.Trim();
        RejectedAt = now;
        return Result.Success();
    }

    public Result MarkShippedBack(string? trackingCode, DateTime now)
    {
        if (Status != ReturnRequestStatus.Approved) return Result.Failure(OrdersErrors.ReturnInvalidTransition);
        Status = ReturnRequestStatus.ShippedBack;
        ReturnTrackingCode = string.IsNullOrWhiteSpace(trackingCode) ? null : trackingCode.Trim();
        ShippedBackAt = now;
        return Result.Success();
    }

    /// <summary>Allows Approved -> Received directly so an admin can mark a return received even if the
    /// customer never called the "shipped back" endpoint.</summary>
    public Result MarkReceived(DateTime now)
    {
        if (Status is not (ReturnRequestStatus.Approved or ReturnRequestStatus.ShippedBack))
            return Result.Failure(OrdersErrors.ReturnInvalidTransition);

        Status = ReturnRequestStatus.Received;
        ReceivedAt = now;
        return Result.Success();
    }

    public Result MarkRefunded(string method, DateTime now)
    {
        if (Status != ReturnRequestStatus.Received) return Result.Failure(OrdersErrors.ReturnInvalidTransition);
        Status = ReturnRequestStatus.Refunded;
        RefundMethod = method;
        RefundedAt = now;
        return Result.Success();
    }
}
