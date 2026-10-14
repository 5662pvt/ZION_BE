using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Contracts.Orders;

public sealed record OrderShippedIntegrationEvent(Guid OrderId, Guid UserId) : IntegrationEvent;
