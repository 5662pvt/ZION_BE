using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Contracts.Orders;

public sealed record OrderCreatedIntegrationEvent(Guid OrderId, Guid UserId, decimal TotalAmount, IReadOnlyList<OrderLine> Lines) : IntegrationEvent;

public sealed record OrderLine(Guid ProductId, int Quantity, decimal UnitPrice);
