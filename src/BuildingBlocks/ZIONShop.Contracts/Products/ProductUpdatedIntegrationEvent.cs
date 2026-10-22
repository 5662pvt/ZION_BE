using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Contracts.Products;

public sealed record ProductVariantSnapshot(Guid VariantId, string Sku, string? Label);

public sealed record ProductUpdatedIntegrationEvent(Guid ProductId, string Name, IReadOnlyList<ProductVariantSnapshot> Variants) : IntegrationEvent;
