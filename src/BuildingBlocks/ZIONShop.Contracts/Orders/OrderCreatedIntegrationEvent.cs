using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Contracts.Orders;

public sealed record OrderCreatedIntegrationEvent(Guid OrderId, Guid UserId, decimal TotalAmount, IReadOnlyList<OrderLine> Lines, string PaymentMethod, string Currency) : IntegrationEvent;

/// <summary>VariantId ?? ProductId is the stock/price key.</summary>
public sealed record OrderLine(Guid ProductId, int Quantity, decimal UnitPrice, bool IsGift = false, Guid? VariantId = null);

public sealed record OrderConfirmedIntegrationEvent(Guid OrderId, Guid UserId) : IntegrationEvent;

public sealed record OrderCancelledIntegrationEvent(Guid OrderId, Guid UserId, string Reason) : IntegrationEvent;
