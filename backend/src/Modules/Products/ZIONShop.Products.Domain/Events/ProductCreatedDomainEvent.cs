using ZIONShop.SharedKernel.Events;

namespace ZIONShop.Products.Domain.Events;

public sealed record ProductCreatedDomainEvent(Guid ProductId, string Sku, string Name) : DomainEvent;
