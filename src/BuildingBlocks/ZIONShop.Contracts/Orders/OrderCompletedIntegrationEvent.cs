using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Contracts.Orders;

public sealed record OrderCompletedIntegrationEvent(Guid OrderId, Guid UserId) : IntegrationEvent;
