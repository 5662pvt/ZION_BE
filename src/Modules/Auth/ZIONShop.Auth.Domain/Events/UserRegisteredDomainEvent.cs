using ZIONShop.SharedKernel.Events;

namespace ZIONShop.Auth.Domain.Events;

public sealed record UserRegisteredDomainEvent(Guid UserId, string Email, string? DisplayName) : DomainEvent;
