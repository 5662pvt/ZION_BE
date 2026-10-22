using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Contracts.Reviews;

public sealed record ReviewCreatedIntegrationEvent(Guid ReviewId, Guid UserId, Guid ProductId, Guid OrderId, int Rating) : IntegrationEvent;
