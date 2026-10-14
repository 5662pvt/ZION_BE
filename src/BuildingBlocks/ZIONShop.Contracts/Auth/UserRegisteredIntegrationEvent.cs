using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Contracts.Auth;

public sealed record UserRegisteredIntegrationEvent(Guid UserId, string Email, string? DisplayName) : IntegrationEvent;
