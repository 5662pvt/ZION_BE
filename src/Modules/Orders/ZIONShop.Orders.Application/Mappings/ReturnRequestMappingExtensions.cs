using ZIONShop.Orders.Application.DTOs;
using ZIONShop.Orders.Domain.Entities;

namespace ZIONShop.Orders.Application.Mappings;

public static class ReturnRequestMappingExtensions
{
    public static ReturnRequestDto ToDto(this ReturnRequest r, string orderCode) => new(
        r.Id,
        r.OrderId,
        orderCode,
        r.Reason.ToString(),
        r.Note,
        r.Status.ToString(),
        r.RejectReason,
        r.ReturnTrackingCode,
        r.RefundAmount,
        r.RefundMethod,
        r.CreatedAt,
        r.ApprovedAt,
        r.RejectedAt,
        r.ShippedBackAt,
        r.ReceivedAt,
        r.RefundedAt);

    public static AdminReturnRequestDto ToAdminDto(this ReturnRequest r, string orderCode, string recipientName, string phoneNumber) => new(
        r.Id,
        r.OrderId,
        orderCode,
        recipientName,
        phoneNumber,
        r.Reason.ToString(),
        r.Note,
        r.Status.ToString(),
        r.RejectReason,
        r.ReturnTrackingCode,
        r.RefundAmount,
        r.RefundMethod,
        r.CreatedAt,
        r.ApprovedAt,
        r.RejectedAt,
        r.ShippedBackAt,
        r.ReceivedAt,
        r.RefundedAt);
}
