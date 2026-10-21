namespace ZIONShop.Orders.Application.DTOs;

public record ReturnRequestDto(
    Guid Id,
    Guid OrderId,
    string OrderCode,
    string Reason,
    string? Note,
    string Status,
    string? RejectReason,
    string? ReturnTrackingCode,
    decimal RefundAmount,
    string? RefundMethod,
    DateTime CreatedAt,
    DateTime? ApprovedAt,
    DateTime? RejectedAt,
    DateTime? ShippedBackAt,
    DateTime? ReceivedAt,
    DateTime? RefundedAt);

public record AdminReturnRequestDto(
    Guid Id,
    Guid OrderId,
    string OrderCode,
    string RecipientName,
    string PhoneNumber,
    string Reason,
    string? Note,
    string Status,
    string? RejectReason,
    string? ReturnTrackingCode,
    decimal RefundAmount,
    string? RefundMethod,
    DateTime CreatedAt,
    DateTime? ApprovedAt,
    DateTime? RejectedAt,
    DateTime? ShippedBackAt,
    DateTime? ReceivedAt,
    DateTime? RefundedAt);
