using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Contracts.Orders;

public sealed record ReturnLine(Guid ProductId, int Quantity);

public sealed record OrderReturnRequestedIntegrationEvent(Guid OrderId, Guid UserId, Guid ReturnRequestId, string Reason) : IntegrationEvent;

public sealed record OrderReturnDecidedIntegrationEvent(Guid OrderId, Guid UserId, Guid ReturnRequestId, bool Approved, string? RejectReason) : IntegrationEvent;

public sealed record OrderReturnShippedBackIntegrationEvent(Guid OrderId, Guid UserId, Guid ReturnRequestId, string? TrackingCode) : IntegrationEvent;

/// <summary>Lines = ALL order lines including gift lines, at full quantities (whole-order return only).</summary>
public sealed record OrderReturnReceivedIntegrationEvent(
    Guid OrderId,
    Guid UserId,
    Guid ReturnRequestId,
    decimal RefundAmount,
    string PaymentMethod,
    IReadOnlyList<ReturnLine> Lines) : IntegrationEvent;

/// <summary>Published by the Payments module once the (simulated) refund settles; consumed by Orders to
/// mark the return request Refunded and the order Returned.</summary>
public sealed record OrderRefundedIntegrationEvent(Guid OrderId, Guid UserId, Guid ReturnRequestId, decimal Amount, string Method) : IntegrationEvent;
