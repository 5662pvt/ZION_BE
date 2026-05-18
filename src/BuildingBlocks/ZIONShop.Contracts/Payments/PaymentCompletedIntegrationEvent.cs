using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Contracts.Payments;

public sealed record PaymentCompletedIntegrationEvent(Guid OrderId, Guid PaymentId, decimal Amount, string Provider) : IntegrationEvent;
