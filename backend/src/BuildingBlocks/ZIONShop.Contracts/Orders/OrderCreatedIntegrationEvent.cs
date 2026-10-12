using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Contracts.Orders;

public sealed record OrderCreatedIntegrationEvent(Guid OrderId, Guid UserId, decimal TotalAmount, IReadOnlyList<OrderLine> Lines, string PaymentMethod, string Currency) : IntegrationEvent;

public sealed record OrderLine(Guid ProductId, int Quantity, decimal UnitPrice, bool IsGift = false);

public sealed record OrderConfirmedIntegrationEvent(Guid OrderId, Guid UserId) : IntegrationEvent;

public sealed record OrderCancelledIntegrationEvent(Guid OrderId, Guid UserId, string Reason) : IntegrationEvent;
